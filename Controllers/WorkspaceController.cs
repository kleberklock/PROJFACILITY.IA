using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using PROJFACILITY.IA.Services;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PROJFACILITY.IA.Controllers
{
    [Route("api/workspace")]
    [ApiController]
    [Authorize(Roles = "admin")]
    public class WorkspaceController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWorkspaceToolService _workspaceToolService;
        private readonly ChatService _chatService;

        public WorkspaceController(AppDbContext context, IWorkspaceToolService workspaceToolService, ChatService chatService)
        {
            _context = context;
            _workspaceToolService = workspaceToolService;
            _chatService = chatService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var agents = await _context.Agents.ToListAsync();
            return Ok(agents);
        }

        [HttpPost("session")]
        public async Task<IActionResult> StartWorkspaceSession([FromBody] StartWorkspaceSessionRequest request)
        {
            var agent = await _context.Agents.FindAsync(request.AgentId);
            if (agent == null)
            {
                return NotFound(new { message = "Agente não encontrado." });
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;
            if (!int.TryParse(userIdStr, out var adminUserId))
            {
                return Unauthorized(new { message = "Usuário inválido ou não autenticado." });
            }

            var session = new WorkspaceSession
            {
                Id = Guid.NewGuid(),
                AdminUserId = adminUserId,
                AgentId = agent.Id,
                StartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                ActiveContext = "{}"
            };

            _context.WorkspaceSessions.Add(session);
            await _context.SaveChangesAsync();

            // Prepara a estrutura para injetar um contexto diferenciado para o agente
            // Override temporário de System Prompt dizendo que ele está no modo de trabalho
            var workspacePromptOverride = $"Você está no modo de trabalho (Workspace) auxiliando um Administrador. {agent.SystemInstruction}";

            return Ok(new 
            { 
                message = "Sessão de Workspace iniciada com sucesso.",
                sessionId = session.Id,
                agentId = agent.Id,
                agentName = agent.Name,
                workspacePrompt = workspacePromptOverride
            });
        }

        [HttpPut("session/context")]
        public async Task<IActionResult> UpdateSessionContext([FromBody] UpdateSessionContextRequest request)
        {
            var session = await _context.WorkspaceSessions.FindAsync(request.SessionId);
            if (session == null)
            {
                return NotFound(new { message = "Sessão de Workspace não encontrada." });
            }

            session.ActiveContext = request.NewContext;
            session.LastActivity = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Contexto atualizado com sucesso." });
        }

        [HttpPost("session/message")]
        public async Task<IActionResult> SendMessageToWorkspace([FromBody] WorkspaceMessageRequest request)
        {
            var session = await _context.WorkspaceSessions.FindAsync(request.SessionId);
            if (session == null)
            {
                return NotFound(new { message = "Sessão de Workspace não encontrada." });
            }

            // Usamos o ActiveContext da sessão como contexto extra de prioridade máxima
            string activeContext = $"[CONTEXTO DE TRABALHO ATUAL]: {session.ActiveContext}";

            // Passamos uma lista vazia de histórico pois o foco é a interação guiada pelo ActiveContext
            var historicoVazio = new System.Collections.Generic.List<ChatMessage>();

            var (aiResponse, tokens) = await _chatService.GetAIResponse(
                request.UserMessage,
                session.AgentId.ToString(),
                historicoVazio,
                session.AdminUserId,
                activeContext,
                System.Threading.CancellationToken.None
            );

            session.LastActivity = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                response = aiResponse,
                tokensUsed = tokens
            });
        }

        public class StartWorkspaceSessionRequest
        {
            public int AgentId { get; set; }
        }

        public class UpdateSessionContextRequest
        {
            public Guid SessionId { get; set; }
            public string NewContext { get; set; } = string.Empty;
        }
    }
}
