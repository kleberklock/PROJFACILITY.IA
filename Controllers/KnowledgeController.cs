using Microsoft.AspNetCore.Mvc;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Data; 
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization; // Necessário para pegar token
using System.Security.Claims; // Necessário para User.Identity

namespace PROJFACILITY.IA.Controllers
{
    [Route("api/knowledge")]
    [ApiController]
    [Authorize] // Garante que só usuários logados mexam em conhecimento
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
                return BadRequest("Profissão não informada.");

            // Se o userId vier zerado do form, tenta pegar do token
            if (userId == 0)
            {
                var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
                if (!string.IsNullOrEmpty(userIdStr)) userId = int.Parse(userIdStr);
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized("Usuário não identificado.");

            long tamanhoEmBytes = file.Length;
            double tamanhoEmMb = tamanhoEmBytes / (1024.0 * 1024.0);

            // Regras de Plano
            if (user.Plan == "Free" || user.Plan == "Iniciante")
            {
                if (tamanhoEmMb > 2) return BadRequest("O plano Iniciante permite arquivos de até 2MB.");
            }
            else if (user.Plan == "Plus")
            {
                if (tamanhoEmMb > 5) return BadRequest("O plano Plus permite arquivos de até 5MB.");
            }

            try
            {
                using var stream = file.OpenReadStream();
                // CORREÇÃO: Passando userId para o serviço
                await _knowledgeService.ProcessarArquivoEIngerir(stream, file.FileName, profession, userId);
                
                return Ok(new { message = "Arquivo processado e aprendido com sucesso!" });
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

            // Pega ID do usuário logado (Token)
            var userIdStr = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            try
            {
                // CORREÇÃO: Passando userId
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