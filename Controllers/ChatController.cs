using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace PROJFACILITY.IA.Controllers
{
    [Authorize]
    [Route("api/chat")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly AppDbContext _context;

        public ChatController(ChatService chatService, AppDbContext context)
        {
            _chatService = chatService;
            _context = context;
        }

        // 1. LISTAR SESSÕES (Histórico Lateral)
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            // Agrupa por SessionId para pegar a última conversa de cada sessão
            var sessions = await _context.ChatMessages
                .Where(m => m.UserId == userId && !string.IsNullOrEmpty(m.SessionId))
                .GroupBy(m => m.SessionId)
                .Select(g => new
                {
                    Id = g.Key,
                    // Pega a primeira mensagem do usuário como Título
                    Title = g.Where(m => m.Sender == "user")
                             .OrderBy(m => m.Timestamp)
                             .Select(m => m.Text)
                             .FirstOrDefault() ?? "Nova Conversa",
                    AgentId = g.FirstOrDefault().AgentId,
                    Timestamp = g.Max(m => m.Timestamp)
                })
                .OrderByDescending(x => x.Timestamp)
                .Take(50) // Limite de 50 conversas no histórico
                .ToListAsync();

            return Ok(sessions);
        }

        // 2. CARREGAR MENSAGENS DE UMA SESSÃO
        [HttpGet("history/{sessionId}")]
        public async Task<IActionResult> GetSessionMessages(string sessionId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var messages = await _context.ChatMessages
                .Where(m => m.UserId == userId && m.SessionId == sessionId)
                .OrderBy(m => m.Timestamp)
                .Select(m => new { m.Sender, m.Text, m.AgentId, m.Timestamp }) // Projeção simples
                .ToListAsync();

            return Ok(messages);
        }

        [HttpPost("enviar")]
        public async Task<IActionResult> EnviarMensagem(
            [FromForm] string message, 
            [FromForm] string agentId, 
            [FromForm] string sessionId,
            CancellationToken ct) // <--- O ASP.NET injeta isso automaticamente quando o usuário cancela
        {
            try 
            {
                var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) 
                    return Unauthorized(new { message = "Usuário não identificado." });

                // Busca Histórico
                var history = await _context.ChatMessages
                    .Where(m => m.UserId == userId && m.SessionId == sessionId) 
                    .OrderByDescending(m => m.Timestamp)
                    .Take(10)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync(ct); // Passa ct

                // Salvar Msg Usuário
                var userMsg = new ChatMessage 
                { 
                    UserId = userId, 
                    AgentId = agentId, 
                    SessionId = sessionId,
                    Text = message, 
                    Sender = "user", 
                    Timestamp = DateTime.UtcNow 
                };
                _context.ChatMessages.Add(userMsg);
                await _context.SaveChangesAsync(ct);
                
                // CHAMA O SERVIÇO PASSANDO O TOKEN
                var (responseAI, tokens) = await _chatService.GetAIResponse(message, agentId, history, userId, ct);

                // Salvar Resposta da IA
                var aiMsg = new ChatMessage 
                { 
                    UserId = userId, 
                    AgentId = agentId, 
                    SessionId = sessionId,
                    Text = responseAI, 
                    Sender = "assistant", 
                    Timestamp = DateTime.UtcNow 
                };
                _context.ChatMessages.Add(aiMsg);
                await _context.SaveChangesAsync(ct);

                return Ok(new { reply = responseAI, tokens = tokens });
            }
            catch (OperationCanceledException)
            {
                // O usuário clicou em Parar. Não é erro, é intencional.
                return StatusCode(499, new { message = "Geração cancelada pelo usuário." }); // 499 é um código comum para Client Closed Request
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro: " + ex.Message });
            }
        }
    }
}