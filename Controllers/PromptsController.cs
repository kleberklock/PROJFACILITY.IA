using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using System.Security.Claims;

namespace PROJFACILITY.IA.Controllers
{
    [Authorize]
    [Route("api/prompts")]
    [ApiController]
    public class PromptsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PromptsController(AppDbContext context)
        {
            _context = context;
        }

        // --- NOVO ENDPOINT: Buscar Prompts do Sistema (ADICIONADO) ---
        [HttpGet("system")]
        [AllowAnonymous] // Permite carregar mesmo se houver problema temporário no token, ou remova se quiser estrito
        public async Task<IActionResult> GetSystemPrompts()
        {
            try
            {
                // Busca os prompts do sistema ordenados para facilitar o agrupamento no front
                var prompts = await _context.SystemPrompts
                    .OrderBy(p => p.Area)
                    .ThenBy(p => p.Profession)
                    .ToListAsync();

                return Ok(prompts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao buscar prompts do sistema", error = ex.Message });
            }
        }
        // -------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> GetMyPrompts()
        {
            // Tenta pegar o ID do ClaimTypes.NameIdentifier (padrão JWT) ou Identity.Name
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) 
                return Unauthorized();

            var prompts = await _context.Prompts
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return Ok(prompts);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePrompt([FromBody] Prompt prompt)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) 
                return Unauthorized();

            prompt.UserId = userId;
            prompt.CreatedAt = DateTime.UtcNow;
            
            _context.Prompts.Add(prompt);
            await _context.SaveChangesAsync();
            return Ok(prompt);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePrompt(int id, [FromBody] Prompt promptUpdate)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) 
                return Unauthorized();

            var prompt = await _context.Prompts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            
            if (prompt == null) return NotFound("Prompt não encontrado ou não pertence a você.");

            // Atualiza apenas os campos permitidos
            prompt.Title = promptUpdate.Title;
            prompt.Content = promptUpdate.Content;
            // Opcional: prompt.IsFavorite = promptUpdate.IsFavorite;

            await _context.SaveChangesAsync();
            return Ok(prompt);
        }
        
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePrompt(int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) 
                return Unauthorized();

            var prompt = await _context.Prompts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            
            if (prompt == null) return NotFound();

            _context.Prompts.Remove(prompt);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}