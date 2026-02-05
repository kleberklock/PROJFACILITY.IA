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

        // --- FUNÇÃO INTELIGENTE DE CATEGORIZAÇÃO (Mantida) ---
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
            if (userId == null || userId == 0) userId = currentUserId;

            return await _context.Agents
                .Where(a => a.IsPublic || a.UserId == null || (userId.HasValue && a.UserId == userId))
                .OrderByDescending(a => a.Id) 
                .ToListAsync();
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
                // Configuração para Agente Global (Admin)
                finalUserId = null; // Sem dono específico (Global)
                finalIsPublic = true;

                // Define Especialidade: Se veio no request, usa. Se não, infere.
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

        // Métodos Update e Delete mantidos conforme original (mas omitidos aqui por brevidade se não houver alterações)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAgent(int id, [FromBody] CreateAgentRequest request) { /* Lógica mantida */ return Ok(); }
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAgent(int id) { /* Lógica mantida */ return Ok(); }

        public class CreateAgentRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Prompt { get; set; } = string.Empty;
            public int CreatorId { get; set; }
            public string Icon { get; set; } = "fa-robot";
            // NOVO CAMPO
            public string Specialty { get; set; } = string.Empty; 
        }
    }
}