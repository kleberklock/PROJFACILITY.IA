using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using PROJFACILITY.IA.Services;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;

namespace PROJFACILITY.IA.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public AuthController(AppDbContext context, IConfiguration configuration, EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        // --- ENDPOINTS DE RECUPERAÇÃO DE SENHA (ESQUECI A SENHA) ---

        [HttpPost("enviar-codigo")]
        public async Task<IActionResult> EnviarCodigo([FromBody] EnviarCodigoRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(new { message = "E-mail é obrigatório." });

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                // Se já existe e está ativo, o usuário deveria fazer login, não cadastrar
                if (user != null && user.IsActive == true)
                    return BadRequest(new { message = "Este e-mail já possui cadastro. Faça login." });

                string codigo = GerarCodigoSeguro();

                if (user == null)
                {
                    user = new User
                    {
                        Email = request.Email,
                        IsActive = false,
                        VerificationCode = codigo,
                        VerificationCodeExpires = DateTime.UtcNow.AddMinutes(15),
                        CreatedAt = DateTime.UtcNow,
                        Name = "Pendente",
                        Password = ""
                    };
                    _context.Users.Add(user);
                }
                else
                {
                    user.VerificationCode = codigo;
                    user.VerificationCodeExpires = DateTime.UtcNow.AddMinutes(15);
                }

                await _context.SaveChangesAsync();
                await EnviarEmailCodigo(user.Email, codigo, "Código de Verificação");

                return Ok(new { message = "Código enviado com sucesso." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro interno: " + ex.Message });
            }
        }

        [HttpPost("solicitar-redefinicao")]
        public async Task<IActionResult> SolicitarRedefinicao([FromBody] EnviarCodigoRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(new { message = "E-mail é obrigatório." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
                return Ok(new { message = "Se o e-mail existir, um código foi enviado." });

            string codigo = GerarCodigoSeguro();
            
            user.VerificationCode = codigo;
            user.VerificationCodeExpires = DateTime.UtcNow.AddMinutes(15);
            
            await _context.SaveChangesAsync();
            await EnviarEmailCodigo(user.Email, codigo, "Redefinição de Senha");

            return Ok(new { message = "Código enviado para seu e-mail." });
        }

        [HttpPost("redefinir-senha")]
        public async Task<IActionResult> RedefinirSenha([FromBody] RedefinirSenhaRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Codigo) || string.IsNullOrEmpty(request.NewPassword))
                return BadRequest(new { message = "Preencha todos os campos." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null) return BadRequest(new { message = "Usuário não encontrado." });
            if (user.VerificationCode != request.Codigo) return BadRequest(new { message = "Código inválido." });
            if (user.VerificationCodeExpires < DateTime.UtcNow) return BadRequest(new { message = "Código expirado." });

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.VerificationCode = null;
            user.IsActive = true; 

            await _context.SaveChangesAsync();

            return Ok(new { message = "Senha alterada com sucesso! Faça login." });
        }

        // --- NOVO: TROCAR SENHA (LOGADO NA TELA DE CONFIGURAÇÕES) ---
        [Authorize]
        [HttpPost("trocar-senha")]
        public async Task<IActionResult> TrocarSenha([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userIdStr = User.Identity?.Name; 
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                    return Unauthorized(new { message = "Token inválido." });

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound(new { message = "Usuário não encontrado." });

                if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password))
                {
                    return BadRequest(new { message = "A senha atual está incorreta." });
                }

                user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Senha atualizada com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro interno: " + ex.Message });
            }
        }

        // --- ENDPOINTS DE REGISTRO E LOGIN ---

        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar([FromBody] RegistrarRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Codigo) || 
                string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Name))
                return BadRequest(new { message = "Todos os campos são obrigatórios." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
                return BadRequest(new { message = "E-mail não encontrado. Solicite o código novamente." });

            if (user.IsActive == true)
                return BadRequest(new { message = "Usuário já cadastrado." });

            if (user.VerificationCode != request.Codigo)
                return BadRequest(new { message = "Código incorreto." });

            if (user.VerificationCodeExpires < DateTime.UtcNow)
                return BadRequest(new { message = "Código expirado. Solicite novamente." });

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            user.Name = request.Name;
            user.Password = passwordHash; 
            user.IsActive = true;
            user.VerificationCode = null;
            user.Plan = "Free";
            
            await _context.SaveChangesAsync();

            // --- INÍCIO DA ADIÇÃO: ALERTA DE NOVA CONTA ---
            _ = Task.Run(async () => 
            {
                try 
                {
                    var mensagem = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; border: 1px solid #e0e0e0; border-radius: 8px; max-width: 600px; margin: 0 auto;'>
                            <h2 style='color: #009966;'>Novo Registo na Plataforma</h2>
                            <p>Uma nova conta foi criada na plataforma.</p>
                            <ul style='list-style-type: none; padding: 0;'>
                                <li><strong>Nome:</strong> {user.Name}</li>
                                <li><strong>E-mail:</strong> {user.Email}</li>
                                <li><strong>Data e Hora:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</li>
                            </ul>
                            <p style='font-size: 12px; color: #888; margin-top: 20px; border-top: 1px solid #eee; padding-top: 10px;'>Este é um alerta automático do Facility.IA.</p>
                        </div>";
                    
                    await _emailService.SendEmailAsync(
                        "klockk27@gmail.com", 
                        "Alerta de Novo Utilizador - Facility.IA", 
                        mensagem
                    );
                } 
                catch 
                {
                    // Ignora o erro silenciosamente para não impedir o registro do usuário se o e-mail falhar
                }
            });
            // --- FIM DA ADIÇÃO ---

            var token = GerarTokenJwt(user);
            return Ok(new { 
                token = token, 
                user = new { user.Id, user.Name, user.Email, Role = "user" } 
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || string.IsNullOrEmpty(user.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password)) 
                return Unauthorized(new { message = "E-mail ou senha inválidos." });

            if (user.IsActive == false)
                return Unauthorized(new { message = "Conta não ativada ou pendente." });

            var token = GerarTokenJwt(user);
            user.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { token, user = new { user.Id, user.Name, user.Email, Role = user.Role } });
        }

        // --- MÉTODOS AUXILIARES ---
        private string GerarCodigoSeguro()
        {
         // Gera um número verdadeiramente aleatório e criptograficamente seguro entre 100000 e 999999
         return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        }
        private string GerarTokenJwt(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
                throw new Exception("FATAL: Jwt:Key não configurada ou muito curta no appsettings.json");

            var key = Encoding.ASCII.GetBytes(jwtKey);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role ?? "user")
                }),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }

        private async Task EnviarEmailCodigo(string email, string codigo, string titulo)
        {
            string html = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                    <h2>{titulo}</h2>
                    <p>Use o código abaixo no Facility.IA:</p>
                    <h1 style='background: #eee; padding: 10px; display: inline-block; letter-spacing: 5px;'>{codigo}</h1>
                    <p>Este código expira em 15 minutos.</p>
                </div>";

            await _emailService.SendEmailAsync(email, $"{titulo} - Facility.IA", html);
        }
    }

   // --- DTOs (Data Transfer Objects) CORRIGIDOS ---
    public class EnviarCodigoRequest 
    { 
        public string Email { get; set; } = string.Empty; 
    }
    
    public class RedefinirSenhaRequest 
    { 
        public string Email { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class RegistrarRequest 
    { 
        public string Email { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest 
    { 
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}