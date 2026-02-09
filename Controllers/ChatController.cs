using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Configuration; 
using Pinecone; 
using System.Linq; 

namespace PROJFACILITY.IA.Controllers
{
    [Authorize]
    [Route("api/chat")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration; 

        public ChatController(ChatService chatService, AppDbContext context, IConfiguration configuration)
        {
            _chatService = chatService;
            _context = context;
            _configuration = configuration;
        }

        // 1. LISTAR SESSÕES
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var sessions = await _context.ChatMessages
                .Where(m => m.UserId == userId && !string.IsNullOrEmpty(m.SessionId))
                .GroupBy(m => m.SessionId)
                .Select(g => new
                {
                    Id = g.Key,
                    Title = g.Where(m => m.Sender == "user")
                             .OrderBy(m => m.Timestamp)
                             .Select(m => m.Text)
                             .FirstOrDefault() ?? "Nova Conversa",
                    AgentId = g.FirstOrDefault()!.AgentId, 
                    Timestamp = g.Max(m => m.Timestamp)
                })
                .OrderByDescending(x => x.Timestamp)
                .Take(50)
                .ToListAsync();

            return Ok(sessions);
        }

        // 2. HISTÓRICO
        [HttpGet("history/{sessionId}")]
        public async Task<IActionResult> GetSessionMessages(string sessionId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var messages = await _context.ChatMessages
                .Where(m => m.UserId == userId && m.SessionId == sessionId)
                .OrderBy(m => m.Timestamp)
                .Select(m => new { m.Sender, m.Text, m.AgentId, m.Timestamp })
                .ToListAsync();

            return Ok(messages);
        }

        // 3. ENVIAR
        [HttpPost("enviar")]
        public async Task<IActionResult> EnviarMensagem(
            [FromForm] string message, 
            [FromForm] string agentId, 
            [FromForm] string sessionId,
            CancellationToken ct)
        {
            try 
            {
                var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) 
                    return Unauthorized(new { message = "Usuário não identificado." });

                // --- ALTERAÇÃO: Validação e Criação de Nova Sessão ---
                // Se o sessionId for inválido ou solicitar nova sessão, gera um novo GUID.
                // Isso é feito ANTES de buscar o histórico para garantir contexto limpo.
                if (string.IsNullOrEmpty(sessionId) || sessionId.ToLower() == "nova" || sessionId == "undefined" || sessionId == "new-session")
                {
                    sessionId = Guid.NewGuid().ToString();
                }
                // -----------------------------------------------------

                var history = await _context.ChatMessages
                    .Where(m => m.UserId == userId && m.SessionId == sessionId) 
                    .OrderByDescending(m => m.Timestamp)
                    .Take(10)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync(ct);

                var userMsg = new ChatMessage 
                { 
                    UserId = userId, AgentId = agentId, SessionId = sessionId,
                    Text = message, Sender = "user", Timestamp = DateTime.UtcNow 
                };
                _context.ChatMessages.Add(userMsg);
                await _context.SaveChangesAsync(ct);
                
                var (responseAI, tokens) = await _chatService.GetAIResponse(message, agentId, history, userId, ct);

                var aiMsg = new ChatMessage 
                { 
                    UserId = userId, AgentId = agentId, SessionId = sessionId,
                    Text = responseAI, Sender = "assistant", Timestamp = DateTime.UtcNow 
                };
                _context.ChatMessages.Add(aiMsg);
                await _context.SaveChangesAsync(ct);

                // Retorna o sessionId (pode ser o novo gerado) para que o front atualize
                return Ok(new { reply = responseAI, tokens = tokens, sessionId = sessionId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro: " + ex.Message });
            }
        }

        // --- 4. DEBUG DE RECUPERAÇÃO REAL (NOVO) ---
        // Acesse no navegador: /api/chat/debug-search?query=codigo&agentName=Agente%20de%20Teste%2001
        [HttpGet("debug-search")]
        public async Task<IActionResult> DebugSearch([FromQuery] string query, [FromQuery] string agentName)
        {
            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var resultado = await _chatService.DebugRetrieval(query, agentName, userId);
            return Ok(resultado);
        }

        // --- 5. DEBUG PINECONE SIMPLES ---
        [HttpGet("debug-pinecone/{agentName}")]
        public async Task<IActionResult> DebugPinecone(string agentName)
        {
            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var resultado = await _chatService.DebugRetrieval("teste", agentName, userId);
            return Ok(resultado);
        }
    }
}