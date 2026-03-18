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
using Microsoft.EntityFrameworkCore;

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
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string? tag, [FromForm] int userId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            if (string.IsNullOrEmpty(tag))
                tag = "Geral";

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
                var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
                bool isAdmin = roleClaim != null && roleClaim.Value == "admin";
                bool isSystemKnowledge = isAdmin && tag.Equals("GlobalSystem", StringComparison.OrdinalIgnoreCase);

                using var stream = file.OpenReadStream();
                
                bool sucesso = await _knowledgeService.ProcessarArquivoEIngerir(stream, file.FileName, tag, userId, isSystemKnowledge);
                
                if (sucesso)
                {
                    return Ok(new { message = $"Arquivo adicionado ao Cérebro Global (Tag: '{tag}') com sucesso!" });
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
            if (string.IsNullOrEmpty(request.Text))
                return BadRequest("Texto inválido.");

            string tag = string.IsNullOrEmpty(request.Tag) ? "Geral" : request.Tag;

            int userId = GetUserId();
            if (userId == 0) return Unauthorized();

            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Text));
                
                await _knowledgeService.IngerirConhecimento(stream, "texto_manual.txt", tag, userId);
                
                return Ok(new { message = "Texto absorvido com sucesso!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erro ao ingerir texto: {ex.Message}" });
            }
        }

        // --- MÉTODOS CORRIGIDOS (Usando .Tag em vez de .Profession) ---

        [HttpGet("files")]
        public async Task<ActionResult> GetFiles()
        {
            int currentUserId = GetUserId();
            
            var files = await _context.KnowledgeDocuments
                .Where(k => k.UserId == currentUserId) 
                .Select(k => new { k.Id, k.FileName, k.Tag, k.CreatedAt }) 
                .OrderByDescending(k => k.Id)
                .ToListAsync();

            return Ok(files);
        }

        [HttpDelete("file/{fileId}")]
        public async Task<IActionResult> DeleteFile(int fileId)
        {
            int currentUserId = GetUserId();
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            var doc = await _context.KnowledgeDocuments.FindAsync(fileId);
            if (doc == null) return NotFound("Arquivo não encontrado.");

            if (doc.UserId != currentUserId && !isAdmin)
            {
                return Forbid("Sem permissão para excluir este arquivo.");
            }

            bool sucesso = isAdmin 
                ? await _knowledgeService.ExcluirArquivo(fileId) 
                : await _knowledgeService.ExcluirArquivo(fileId, currentUserId);
            
            if (sucesso)
                return Ok(new { message = "Arquivo removido da base de conhecimento e do motor de busca." });
            else
                return BadRequest(new { message = "Erro ao remover arquivo do motor de busca." });
        }

        public class IngestTextRequest
        {
            public string Text { get; set; } = string.Empty;
            public string? Tag { get; set; } = string.Empty;
        }
    }
}