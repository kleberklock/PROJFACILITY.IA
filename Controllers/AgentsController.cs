using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace PROJFACILITY.IA.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/agents")]
    public class AgentsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AgentsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id") ?? User.FindFirst(ClaimTypes.Name);
            if (idClaim != null && int.TryParse(idClaim.Value, out int idParsed))
            {
                return idParsed;
            }
            return 0;
        }

        // LISTAR
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Agent>>> GetAgents([FromQuery] int? userId)
        {
            int currentUserId = GetUserId();

            if (userId == null || userId == 0)
            {
                userId = currentUserId;
            }

            // ALTERAÇÃO: Agora retorna também se IsPublic for true
            // Isso faz com que agentes criados pelo Admin apareçam para todos
            return await _context.Agents
                .Where(a => a.IsPublic || a.UserId == null || (userId.HasValue && a.UserId == userId))
                .OrderByDescending(a => a.Id) 
                .ToListAsync();
        }

        // CRIAR
        [HttpPost]
        public async Task<IActionResult> PostAgent([FromBody] CreateAgentRequest request)
        {
            int userId = GetUserId();
            
            if (userId == 0 && request.CreatorId > 0) 
                userId = request.CreatorId;

            if (userId == 0) return Unauthorized("Usuário inválido.");

            // VERIFICAÇÃO DE ADMIN
            // Se o usuário tiver a role 'admin', o agente se torna Público automaticamente
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            var agent = new Agent
            {
                Name = request.Name,
                Specialty = "Personalizado",
                SystemInstruction = request.Prompt,
                UserId = userId,
                // Se for Admin, é Público (visível para todos). Se não, é Privado.
                IsPublic = isAdmin, 
                // Usa o ícone enviado pelo front, ou o padrão
                Icon = !string.IsNullOrEmpty(request.Icon) ? request.Icon : "fa-robot"
            };

            _context.Agents.Add(agent);
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Criado com sucesso!", id = agent.Id, isPublic = agent.IsPublic });
        }

        // EDITAR
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAgent(int id, [FromBody] CreateAgentRequest request)
        {
            int userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var agent = await _context.Agents.FindAsync(id);
            if (agent == null) return NotFound();
            
            // Permite edição se for o dono OU se for admin
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            if (agent.UserId != userId && !isAdmin) return Forbid();

            agent.Name = request.Name;
            agent.SystemInstruction = request.Prompt;
            if(!string.IsNullOrEmpty(request.Icon)) agent.Icon = request.Icon;
            
            await _context.SaveChangesAsync();
            return Ok(new { message = "Atualizado!" });
        }

        // EXCLUIR
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAgent(int id)
        {
            int userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var agent = await _context.Agents.FindAsync(id);
            if (agent == null) return NotFound();

            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            if (agent.UserId != userId && !isAdmin) return Forbid();

            _context.Agents.Remove(agent);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Excluído!" });
        }

        public class CreateAgentRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Prompt { get; set; } = string.Empty;
            public int CreatorId { get; set; }
            public string Icon { get; set; } = "fa-robot"; // Adicionado suporte a ícone
        }
    }
}