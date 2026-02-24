using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using System.Security.Claims;
using System.IO; // <--- ADICIONADO: Necessário para FileStream e Path
using Microsoft.AspNetCore.Hosting;

namespace PROJFACILITY.IA.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public UserController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userIdStr = User.Identity?.Name;
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                    return Unauthorized();

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound();

                return Ok(new 
                { 
                    user.Name, 
                    user.Email, 
                    user.Plan, 
                    user.Role,
                    user.ProfilePicture 
                });
            }
            catch
            {
                return StatusCode(500, new { message = "Erro ao buscar perfil." });
            }
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserRequest request)
        {
            try
            {
                var userIdStr = User.Identity?.Name;
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                    return Unauthorized();

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound();

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { message = "O nome não pode ser vazio." });

                user.Name = request.Name;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Perfil atualizado com sucesso!", user = new { user.Name, user.Email } });
            }
            catch
            {
                return StatusCode(500, new { message = "Erro ao atualizar perfil." });
            }
        }

        [HttpPost("upload-photo")]
        [Authorize]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            var userIdStr = User.Identity?.Name;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized();

            if (file == null || file.Length == 0) return BadRequest("Nenhuma imagem enviada.");

            var path = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            var fileName = $"{userId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(path, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var user = await _context.Users.FindAsync(userId);
            user.ProfilePicture = $"/uploads/profiles/{fileName}";
            await _context.SaveChangesAsync();

            return Ok(new { url = user.ProfilePicture });
        }
    }
}