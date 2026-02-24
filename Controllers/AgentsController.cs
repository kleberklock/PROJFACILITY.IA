using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

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

            if (texto.Contains("dev") || texto.Contains("programad") || texto.Contains("software") || 
                texto.Contains("c#") || texto.Contains("python") || texto.Contains("java") || 
                texto.Contains("ti ") || texto.Contains("codigo") || texto.Contains("fullstack") || 
                texto.Contains("dados") || texto.Contains("react") || texto.Contains("sql"))
                return "tecnologia"; 

            if (texto.Contains("medic") || texto.Contains("saude") || texto.Contains("enferm") || 
                texto.Contains("nutri") || texto.Contains("psico") || texto.Contains("terapia") || 
                texto.Contains("fisio") || texto.Contains("clinica"))
                return "saude"; 

            if (texto.Contains("advoga") || texto.Contains("jurid") || texto.Contains("lei") || 
                texto.Contains("direito") || texto.Contains("contrato") || texto.Contains("penal") || 
                texto.Contains("civil") || texto.Contains("oab"))
                return "juridico"; 

            if (texto.Contains("market") || texto.Contains("design") || texto.Contains("copy") || 
                texto.Contains("video") || texto.Contains("social") || texto.Contains("insta") || 
                texto.Contains("trafego") || texto.Contains("arte"))
                return "criativos";

            if (texto.Contains("engenh") || texto.Contains("obra") || texto.Contains("civil") || 
                texto.Contains("eletric") || texto.Contains("arquitet") || texto.Contains("projeto"))
                return "engenharia";

            if (texto.Contains("financ") || texto.Contains("contabil") || texto.Contains("invest") || 
                texto.Contains("econom") || texto.Contains("impost") || texto.Contains("gestao") ||
                texto.Contains("lider") || texto.Contains("adm"))
                return "negocios";

            if (texto.Contains("profess") || texto.Contains("aulas") || texto.Contains("ensino") || 
                texto.Contains("aluno") || texto.Contains("pedagog") || texto.Contains("curso"))
                return "educacao";

            return "outros"; 
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Agent>>> GetAgents([FromQuery] int? userId)
        {
            int currentUserId = GetUserId();
            // Se userId for passado (filtro), usa ele, senão usa o atual
            if (userId == null || userId == 0) userId = currentUserId;

            // Admin vê tudo? Depende da regra. Aqui vou manter que usuário vê os seus + públicos.
            // Se quiser que o admin veja LITERALMENTE TODOS no painel, a query muda um pouco.
            
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            if (isAdmin)
            {
                 return await _context.Agents
                    .OrderByDescending(a => a.Id)
                    .ToListAsync();
            }

            return await _context.Agents
                .Where(a => a.IsPublic || a.UserId == null || (a.UserId == currentUserId))
                .OrderByDescending(a => a.Id) 
                .ToListAsync();
        }

        // NOVO: Método para buscar UM agente específico (usado na edição)
        [HttpGet("{id}")]
        public async Task<ActionResult<Agent>> GetAgent(int id)
        {
            var agent = await _context.Agents.FindAsync(id);

            if (agent == null) return NotFound("Agente não encontrado.");

            // Verifica permissão
            int currentUserId = GetUserId();
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            if (!agent.IsPublic && agent.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }

            return agent;
        }

        [HttpPost]
        public async Task<IActionResult> PostAgent([FromBody] CreateAgentRequest request)
        {
            int userId = GetUserId();
            
            if (userId == 0 && request.CreatorId > 0) userId = request.CreatorId;
            if (userId == 0) return Unauthorized("Usuário inválido.");

            // Verifica se é Admin
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            string finalSpecialty;
            int? finalUserId;
            bool finalIsPublic;

            if (isAdmin)
            {
                // Configuração para Agente Global ou Pessoal (Admin)
                finalUserId = request.IsGlobal ? null : (int?)userId;
                finalIsPublic = request.IsGlobal;

                if (!string.IsNullOrEmpty(request.Specialty) && request.Specialty != "automatico")
                {
                    finalSpecialty = request.Specialty.ToLower();
                }
                else
                {
                    finalSpecialty = InferirEspecialidade(request.Name, request.Prompt);
                }
            }
            else
            {
                // Configuração para Agente Pessoal (Usuário Comum)
                finalUserId = userId;
                finalIsPublic = false;
                finalSpecialty = "Personalizado";
            }

            var agent = new Agent
            {
                Name = request.Name,
                Specialty = finalSpecialty,
                SystemInstruction = request.Prompt,
                UserId = finalUserId,
                IsPublic = finalIsPublic,
                Icon = !string.IsNullOrEmpty(request.Icon) ? request.Icon : "fa-robot"
            };

            _context.Agents.Add(agent);
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Criado com sucesso!", id = agent.Id, specialty = agent.Specialty });
        }

        // --- MÉTODOS DE ATUALIZAÇÃO E EXCLUSÃO CORRIGIDOS ---

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAgent(int id, [FromBody] CreateAgentRequest request) 
        { 
            var agent = await _context.Agents.FindAsync(id);
            if (agent == null) return NotFound("Agente não encontrado.");

            int currentUserId = GetUserId();
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            // Apenas o dono ou Admin pode editar
            // Se agent.UserId for null, é do sistema (só admin edita)
            if (agent.UserId != currentUserId && !isAdmin && agent.UserId != null)
            {
                return Forbid("Você não tem permissão para editar este agente.");
            }
            if (agent.UserId == null && !isAdmin)
            {
                 return Forbid("Apenas administradores podem editar agentes do sistema.");
            }

            // Atualiza os campos
            agent.Name = request.Name;
            agent.SystemInstruction = request.Prompt;
            agent.Icon = request.Icon;
            
            // Admin pode forçar a especialidade
            if (!string.IsNullOrEmpty(request.Specialty) && request.Specialty != "automatico")
            {
                agent.Specialty = request.Specialty.ToLower();
            }
            else if (string.IsNullOrEmpty(agent.Specialty))
            {
                 // Se estiver vazio por algum motivo, re-infere
                 agent.Specialty = InferirEspecialidade(request.Name, request.Prompt);
            }

            _context.Entry(agent).State = EntityState.Modified;
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Agents.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return Ok(new { message = "Agente atualizado com sucesso!" });
        }
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAgent(int id) 
        { 
            var agent = await _context.Agents.FindAsync(id);
            if (agent == null) return NotFound("Agente não encontrado.");

            int currentUserId = GetUserId();
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            // Verificação de segurança
            if (agent.UserId != currentUserId && !isAdmin && agent.UserId != null)
            {
                return Forbid("Você não tem permissão para excluir este agente.");
            }
            if (agent.UserId == null && !isAdmin)
            {
                 return Forbid("Apenas administradores podem excluir agentes do sistema.");
            }

            // Aqui você pode adicionar lógica para remover KnowledgeDocuments vinculados antes, se necessário.
            
            _context.Agents.Remove(agent);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Agente excluído com sucesso!" });
        }

        public class CreateAgentRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Prompt { get; set; } = string.Empty;
            public int CreatorId { get; set; }
            public string Icon { get; set; } = "fa-robot";
            public string Specialty { get; set; } = string.Empty; 
            public bool IsGlobal { get; set; } = false;
        }
    }
}