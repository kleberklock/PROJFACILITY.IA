using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

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

        // --- FUNÇÃO INTELIGENTE DE CATEGORIZAÇÃO ---
        private string InferirEspecialidade(string nome, string prompt)
        {
            var texto = (nome + " " + prompt).ToLower();

            // TECNOLOGIA
            if (texto.Contains("dev") || texto.Contains("programad") || texto.Contains("software") || 
                texto.Contains("c#") || texto.Contains("python") || texto.Contains("java") || 
                texto.Contains("ti ") || texto.Contains("codigo") || texto.Contains("fullstack") || 
                texto.Contains("dados") || texto.Contains("react") || texto.Contains("sql"))
                return "tecnologia"; 

            // SAÚDE (Medicina, Psicologia, etc)
            if (texto.Contains("medic") || texto.Contains("saude") || texto.Contains("enferm") || 
                texto.Contains("nutri") || texto.Contains("psico") || texto.Contains("terapia") || 
                texto.Contains("fisio") || texto.Contains("clinica"))
                return "saude"; 

            // JURÍDICO
            if (texto.Contains("advoga") || texto.Contains("jurid") || texto.Contains("lei") || 
                texto.Contains("direito") || texto.Contains("contrato") || texto.Contains("penal") || 
                texto.Contains("civil") || texto.Contains("oab"))
                return "juridico"; 

            // CRIATIVOS (Marketing, Design, Copy)
            if (texto.Contains("market") || texto.Contains("design") || texto.Contains("copy") || 
                texto.Contains("video") || texto.Contains("social") || texto.Contains("insta") || 
                texto.Contains("trafego") || texto.Contains("arte"))
                return "criativos";

            // ENGENHARIA & OBRAS
            if (texto.Contains("engenh") || texto.Contains("obra") || texto.Contains("civil") || 
                texto.Contains("eletric") || texto.Contains("arquitet") || texto.Contains("projeto"))
                return "engenharia";

            // NEGÓCIOS (Finanças, Contabilidade, Gestão)
            if (texto.Contains("financ") || texto.Contains("contabil") || texto.Contains("invest") || 
                texto.Contains("econom") || texto.Contains("impost") || texto.Contains("gestao") ||
                texto.Contains("lider") || texto.Contains("adm"))
                return "negocios";

            // EDUCAÇÃO
            if (texto.Contains("profess") || texto.Contains("aulas") || texto.Contains("ensino") || 
                texto.Contains("aluno") || texto.Contains("pedagog") || texto.Contains("curso"))
                return "educacao";

            // Padrão
            return "outros"; 
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Agent>>> GetAgents([FromQuery] int? userId)
        {
            int currentUserId = GetUserId();

            if (userId == null || userId == 0)
            {
                userId = currentUserId;
            }

            // Retorna agentes públicos (UserId == null) OU agentes do utilizador atual
            return await _context.Agents
                .Where(a => a.IsPublic || a.UserId == null || (userId.HasValue && a.UserId == userId))
                .OrderByDescending(a => a.Id) 
                .ToListAsync();
        }

        [HttpPost]
        public async Task<IActionResult> PostAgent([FromBody] CreateAgentRequest request)
        {
            int userId = GetUserId();
            
            // Fallback para creatorId vindo do front, se necessário
            if (userId == 0 && request.CreatorId > 0) 
                userId = request.CreatorId;

            if (userId == 0) return Unauthorized("Usuário inválido.");

            // Verifica se é Admin
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            // --- LÓGICA AUTOMÁTICA ---
            string especialidadeAutomatica;
            
            if (isAdmin)
            {
                // Se for admin, usa a IA interna para definir a categoria correta
                especialidadeAutomatica = InferirEspecialidade(request.Name, request.Prompt);
            }
            else
            {
                // Se for utilizador comum, continua como Personalizado
                especialidadeAutomatica = "Personalizado";
            }

            var agent = new Agent
            {
                Name = request.Name,
                Specialty = especialidadeAutomatica,
                SystemInstruction = request.Prompt,
                // LÓGICA GLOBAL: Se admin, UserId é nulo (global). Se user, usa o ID dele.
                UserId = isAdmin ? (int?)null : userId, 
                IsPublic = isAdmin, // Se for admin, já nasce público
                Icon = !string.IsNullOrEmpty(request.Icon) ? request.Icon : "fa-robot"
            };

            _context.Agents.Add(agent);
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Criado com sucesso!", id = agent.Id, specialty = agent.Specialty });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAgent(int id, [FromBody] CreateAgentRequest request)
        {
            int userId = GetUserId();
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            if (userId == 0) return Unauthorized();

            var agent = await _context.Agents.FindAsync(id);
            if (agent == null) return NotFound();

            // Permite edição se for o dono OU se for admin
            if (agent.UserId != userId && !isAdmin) return Forbid();

            agent.Name = request.Name;
            agent.SystemInstruction = request.Prompt;
            if(!string.IsNullOrEmpty(request.Icon)) agent.Icon = request.Icon;
            
            // Se admin editar, pode re-inferir a categoria
            if (isAdmin) {
                 agent.Specialty = InferirEspecialidade(request.Name, request.Prompt);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Atualizado!" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAgent(int id)
        {
            int userId = GetUserId();
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            if (userId == 0) return Unauthorized();

            var agent = await _context.Agents.FindAsync(id);
            if (agent == null) return NotFound();

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
            public string Icon { get; set; } = "fa-robot";
        }
    }
}