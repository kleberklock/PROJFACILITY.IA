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

        // --- 1. ENDPOINT DE PROMPTS DO SISTEMA (Responsável pela aba 'Sistema') ---
        [HttpGet("system")]
        [AllowAnonymous] 
        public async Task<IActionResult> GetSystemPrompts()
        {
            try
            {
                var prompts = await _context.SystemPrompts
                    .OrderBy(p => p.Area)
                    .ThenBy(p => p.Profession)
                    .ToListAsync();

                return Ok(prompts);
            }
            catch (Exception ex)
            {
                // Se der erro aqui, geralmente é porque a tabela SystemPrompts não existe
                return StatusCode(500, new { message = "Erro no banco de dados", error = ex.Message });
            }
        }

        // --- 2. SEUS PROMPTS (Responsável pela aba 'Meus Prompts') ---
        [HttpGet]
        public async Task<IActionResult> GetMyPrompts()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var prompts = await _context.Prompts
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return Ok(prompts);
        }

        // ... (Mantenha os métodos Create, Update e Delete abaixo como estavam)
        [HttpPost]
        public async Task<IActionResult> CreatePrompt([FromBody] Prompt prompt)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

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
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var prompt = await _context.Prompts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (prompt == null) return NotFound();

            prompt.Title = promptUpdate.Title;
            prompt.Content = promptUpdate.Content;
            await _context.SaveChangesAsync();
            return Ok(prompt);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePrompt(int id)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var prompt = await _context.Prompts.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (prompt == null) return NotFound();

            _context.Prompts.Remove(prompt);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}