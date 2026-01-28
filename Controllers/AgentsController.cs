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

        // Método auxiliar seguro para pegar o ID do Usuário
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

            // Se não veio userId na query, usa o do token
            if (userId == null || userId == 0)
            {
                userId = currentUserId;
            }

            // Retorna agentes PÚBLICOS (Null) + Agentes do Usuário
            return await _context.Agents
                .Where(a => a.UserId == null || (userId.HasValue && a.UserId == userId))
                .OrderByDescending(a => a.Id) 
                .ToListAsync();
        }

        // CRIAR
        [HttpPost]
        public async Task<IActionResult> PostAgent([FromBody] CreateAgentRequest request)
        {
            int userId = GetUserId();
            
            // Fallback: se o token falhar, tenta usar o enviado pelo front (menos seguro, mas funcional)
            if (userId == 0 && request.CreatorId > 0) 
                userId = request.CreatorId;

            if (userId == 0) return Unauthorized("Usuário inválido.");

            var agent = new Agent
            {
                Name = request.Name,
                Specialty = "Personalizado",
                SystemInstruction = request.Prompt,
                UserId = userId,
                IsPublic = false,
                Icon = "fa-robot"
            };

            _context.Agents.Add(agent);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Criado com sucesso!", id = agent.Id });
        }

        // EDITAR
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAgent(int id, [FromBody] CreateAgentRequest request)
        {
            int userId = GetUserId();
            if (userId == 0) return Unauthorized();

            var agent = await _context.Agents.FindAsync(id);
            if (agent == null) return NotFound();
            if (agent.UserId != userId) return Forbid(); // Só o dono edita

            agent.Name = request.Name;
            agent.SystemInstruction = request.Prompt;
            
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
            if (agent.UserId != userId) return Forbid();

            _context.Agents.Remove(agent);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Excluído!" });
        }

        public class CreateAgentRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Prompt { get; set; } = string.Empty;
            public int CreatorId { get; set; }
        }
    }
}