using Microsoft.AspNetCore.Mvc;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Data; 
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization; 
using System.Security.Claims; 

namespace PROJFACILITY.IA.Controllers
{
    [Route("api/knowledge")]
    [ApiController]
    [Authorize]
    public class KnowledgeController : ControllerBase
    {
        private readonly KnowledgeService _knowledgeService;
        private readonly AppDbContext _context; 

        public KnowledgeController(KnowledgeService knowledgeService, AppDbContext context)
        {
            _knowledgeService = knowledgeService;
            _context = context;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string profession, [FromForm] int userId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            if (string.IsNullOrEmpty(profession))
                return BadRequest("Profissão (Nome do Agente) não informada.");

            // Recupera UserID com segurança se não vier no form
            if (userId == 0)
            {
                var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id") ?? User.FindFirst(ClaimTypes.Name);
                if (idClaim != null && int.TryParse(idClaim.Value, out int idParsed)) userId = idParsed;
            }
            if (userId == 0) return Unauthorized("Usuário não identificado.");

            var user = await _context.Users.FindAsync(userId);
            
            // Regras de Limite de Plano
            if (user != null)
            {
                long tamanhoEmBytes = file.Length;
                double tamanhoEmMb = tamanhoEmBytes / (1024.0 * 1024.0);

                if (user.Plan == "Free" || user.Plan == "Iniciante")
                {
                    if (tamanhoEmMb > 2) return BadRequest("O plano Iniciante permite arquivos de até 2MB.");
                }
                else if (user.Plan == "Plus")
                {
                    if (tamanhoEmMb > 5) return BadRequest("O plano Plus permite arquivos de até 5MB.");
                }
            }

            try
            {
                // 1. BUSCA O AGENTE NO BANCO PARA VALIDAR PROPRIEDADE
                // Procuramos o agente pelo nome. 
                // Prioridade: Agente do Sistema OU Agente do Usuário (para evitar colisão de nomes com outros users)
                var targetAgent = _context.Agents
                    .FirstOrDefault(a => a.Name == profession && (a.UserId == null || a.UserId == userId));

                if (targetAgent == null)
                {
                    return NotFound($"O agente '{profession}' não foi encontrado ou você não tem acesso a ele.");
                }

                // 2. DEFINE SE É CONHECIMENTO DE SISTEMA OU PRIVADO
                bool isSystemKnowledge = (targetAgent.UserId == null);

                // 3. BLOQUEIO DE SEGURANÇA (O Pulo do Gato)
                if (!isSystemKnowledge)
                {
                    // Se o agente tem dono (UserId != null), TEM que ser o mesmo usuário logado
                    if (targetAgent.UserId != userId)
                    {
                        return StatusCode(403, "Você não tem permissão para treinar este agente. Ele pertence a outro usuário.");
                    }
                }
                else
                {
                    // Se for agente de sistema, opcionalmente checar se é admin
                    // if (user.Role != "admin") return Forbid("Apenas admins podem treinar agentes do sistema.");
                }

                using var stream = file.OpenReadStream();
                
                // Passando a flag correta para o serviço (Isso define se salva como 'system' ou 'userId' no Pinecone)
                await _knowledgeService.ProcessarArquivoEIngerir(stream, file.FileName, profession, userId, isSystemKnowledge);
                
                return Ok(new { message = $"Arquivo adicionado ao cérebro do agente '{profession}' com sucesso!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erro ao processar arquivo: {ex.Message}" });
            }
        }

        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromBody] IngestTextRequest request)
        {
            if (string.IsNullOrEmpty(request.Text) || string.IsNullOrEmpty(request.Profession))
                return BadRequest("Texto ou profissão inválidos.");

            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            try
            {
                // Ingestão manual por enquanto é sempre privada
                await _knowledgeService.IngerirConhecimento(request.Text, request.Profession, "Texto Manual", userId);
                return Ok(new { message = "Texto absorvido com sucesso!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erro ao ingerir texto: {ex.Message}" });
            }
        }

        public class IngestTextRequest
        {
            public string Text { get; set; } = string.Empty;
            public string Profession { get; set; } = string.Empty;
        }
    }
}