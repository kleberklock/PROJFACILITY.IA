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
                
                // O Service usa 'Tag' internamente para salvar a 'profession'
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
                
                await _knowledgeService.IngerirConhecimento(stream, "texto_manual.txt", request.Profession, userId);
                
                return Ok(new { message = "Texto absorvido com sucesso!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Erro ao ingerir texto: {ex.Message}" });
            }
        }

        // --- MÉTODOS CORRIGIDOS (Usando .Tag em vez de .Profession) ---

        [HttpGet("{agentId}/files")]
        public async Task<ActionResult> GetFiles(int agentId)
        {
            int currentUserId = GetUserId();
            
            // Verifica se o agente existe
            var agent = await _context.Agents.FindAsync(agentId);
            if (agent == null) return NotFound("Agente não encontrado.");

            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

            // Valida permissão
            if (agent.UserId != null && agent.UserId != currentUserId && !isAdmin)
            {
                return Forbid();
            }

            // CORREÇÃO AQUI: Usa 'k.Tag' em vez de 'k.Profession'
            var files = await _context.KnowledgeDocuments
                .Where(k => k.Tag == agent.Name) 
                .Select(k => new { k.Id, k.FileName, k.CreatedAt }) 
                .OrderByDescending(k => k.Id)
                .ToListAsync();

            return Ok(files);
        }

        [HttpDelete("file/{fileId}")]
        public async Task<IActionResult> DeleteFile(int fileId)
        {
            var doc = await _context.KnowledgeDocuments.FindAsync(fileId);
            if (doc == null) return NotFound("Arquivo não encontrado.");

            // CORREÇÃO AQUI: Usa 'doc.Tag' para achar o agente
            var agent = await _context.Agents.FirstOrDefaultAsync(a => a.Name == doc.Tag);
            
            if (agent != null)
            {
                int currentUserId = GetUserId();
                var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
                bool isAdmin = roleClaim != null && roleClaim.Value == "admin";

                // Se o agente tem dono e não é o usuário atual (e nem admin), bloqueia
                if (agent.UserId != null && agent.UserId != currentUserId && !isAdmin)
                {
                    return Forbid("Sem permissão para alterar este agente.");
                }
            }

            // Remove do banco (O arquivo físico/vetor idealmente seria removido via Service, 
            // mas aqui estamos removendo o registro do banco diretamente para simplificar o Controller,
            // ou você pode chamar _knowledgeService.ExcluirArquivo se tiver implementado lá)
            _context.KnowledgeDocuments.Remove(doc);
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Arquivo removido da base de conhecimento." });
        }

        public class IngestTextRequest
        {
            public string Text { get; set; } = string.Empty;
            public string Profession { get; set; } = string.Empty;
        }
    }
}