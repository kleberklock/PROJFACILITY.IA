using Microsoft.AspNetCore.Mvc;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Data; 
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization; 
using System.Security.Claims; 
using System.IO;
using System.Text;

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

        private int GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("id") ?? User.FindFirst(ClaimTypes.Name);
            if (idClaim != null && int.TryParse(idClaim.Value, out int idParsed))
            {
                return idParsed;
            }
            return 0;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string profession, [FromForm] int userId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            if (string.IsNullOrEmpty(profession))
                return BadRequest("Profissão (Nome do Agente) não informada.");

            if (userId == 0) userId = GetUserId();
            if (userId == 0) return Unauthorized("Usuário não identificado no Token.");

            var user = await _context.Users.FindAsync(userId);
            
            // Regras de Limite de Plano
            if (user != null)
            {
                long tamanhoEmBytes = file.Length;
                double tamanhoEmMb = tamanhoEmBytes / (1024.0 * 1024.0);

                if (user.Plan == "Iniciante" && tamanhoEmMb > 2) 
                    return BadRequest("O plano Iniciante permite arquivos de até 2MB.");
                else if (user.Plan == "Plus" && tamanhoEmMb > 5) 
                    return BadRequest("O plano Plus permite arquivos de até 5MB.");
            }

            try
            {
                var targetAgent = _context.Agents
                    .FirstOrDefault(a => a.Name == profession && (a.UserId == null || a.UserId == userId));

                if (targetAgent == null)
                    return NotFound($"O agente '{profession}' não foi encontrado ou você não tem acesso a ele.");

                bool isSystemKnowledge = (targetAgent.UserId == null);

                if (!isSystemKnowledge && targetAgent.UserId != userId)
                    return StatusCode(403, "Você não tem permissão para treinar este agente.");

                using var stream = file.OpenReadStream();
                
                // MUDANÇA: Verifica o retorno booleano
                bool sucesso = await _knowledgeService.ProcessarArquivoEIngerir(stream, file.FileName, profession, userId, isSystemKnowledge);
                
                if (sucesso)
                {
                    return Ok(new { message = $"Arquivo adicionado ao cérebro do agente '{profession}' com sucesso!" });
                }
                else
                {
                    return BadRequest(new { message = "Falha ao processar o arquivo. Formato não suportado (use .DOCX, não .DOC) ou arquivo vazio/corrompido." });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro Upload: {ex.Message}");
                return BadRequest(new { message = $"Erro técnico: {ex.Message}" });
            }
        }

        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromBody] IngestTextRequest request)
        {
            if (string.IsNullOrEmpty(request.Text) || string.IsNullOrEmpty(request.Profession))
                return BadRequest("Texto ou profissão inválidos.");

            int userId = GetUserId();
            if (userId == 0) return Unauthorized();

            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Text));
                
                // Agora o método retorna bool, mas aqui podemos ignorar ou checar
                await _knowledgeService.IngerirConhecimento(stream, "texto_manual.txt", request.Profession, userId);
                
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