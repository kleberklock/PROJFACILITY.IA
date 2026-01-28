using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Configuration; // Necessário para ler appsettings
using Pinecone; // Necessário para o Debug
using System.Linq; // Necessário para listas e Count()

namespace PROJFACILITY.IA.Controllers
{
    [Authorize]
    [Route("api/chat")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration; // Variável para a chave da API

        // Construtor atualizado
        public ChatController(ChatService chatService, AppDbContext context, IConfiguration configuration)
        {
            _chatService = chatService;
            _context = context;
            _configuration = configuration;
        }

        // 1. LISTAR SESSÕES (Histórico Lateral)
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            // Correção de warning CS8602: Uso de '?' e verificação segura
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
                    AgentId = g.FirstOrDefault()!.AgentId, // '!' garante que não é nulo (banco garante)
                    Timestamp = g.Max(m => m.Timestamp)
                })
                .OrderByDescending(x => x.Timestamp)
                .Take(50)
                .ToListAsync();

            return Ok(sessions);
        }

        // 2. CARREGAR HISTÓRICO
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

        // 3. ENVIAR MENSAGEM
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

                // Busca contexto (últimas 10 msgs)
                var history = await _context.ChatMessages
                    .Where(m => m.UserId == userId && m.SessionId == sessionId) 
                    .OrderByDescending(m => m.Timestamp)
                    .Take(10)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync(ct);

                // Salva pergunta do usuário
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
                
                // Gera resposta da IA
                var (responseAI, tokens) = await _chatService.GetAIResponse(message, agentId, history, userId, ct);

                // Salva resposta da IA
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
                return StatusCode(499, new { message = "Cancelado pelo usuário." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro: " + ex.Message });
            }
        }

        // --- 4. DEBUG PINECONE (CORRIGIDO) ---
        [HttpGet("debug-pinecone/{agentName}")]
        public async Task<IActionResult> DebugPinecone(string agentName)
        {
            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var pineconeKey = _configuration["Pinecone:ApiKey"];
            if (string.IsNullOrEmpty(pineconeKey)) return BadRequest("Sem chave Pinecone.");

            try 
            {
                var pinecone = new PineconeClient(pineconeKey);
                var index = pinecone.Index("facility-ia");

                // Filtros de segurança
                var filtroUserId = new Metadata();
                filtroUserId.Add("$in", new List<string> { userId.ToString(), "system" });

                var filtroPrincipal = new Metadata();
                filtroPrincipal.Add("tag", agentName);
                filtroPrincipal.Add("userId", filtroUserId);

                // Busca dummy apenas para listar o que tem lá
                var dummyVector = new float[1536]; 
                var search = await index.QueryAsync(new QueryRequest 
                { 
                    Vector = dummyVector, 
                    TopK = 10, 
                    IncludeMetadata = true,
                    Filter = filtroPrincipal 
                });

                // Normaliza para IEnumerable para evitar erros de nulidade
                var matches = search.Matches ?? new List<ScoredVector>(); 

                var arquivosEncontrados = matches
                    .Where(m => m.Metadata != null && m.Metadata.ContainsKey("filename"))
                    .Select(m => m.Metadata!["filename"]!.ToString())
                    .Distinct()
                    .ToList();

                return Ok(new { 
                    Status = "Sucesso",
                    Agente = agentName, 
                    ArquivosNoCerebro = arquivosEncontrados,
                    TotalTrechos = matches.Count() // <--- CORREÇÃO AQUI: Adicionado () pois é um método LINQ
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Erro = ex.Message });
            }
        }
    }
}