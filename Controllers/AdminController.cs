using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using PROJFACILITY.IA.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PROJFACILITY.IA.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "admin")] // Descomente em produção para segurança
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly KnowledgeService _knowledgeService;
        private readonly EmailService _emailService;

        public AdminController(AppDbContext context, KnowledgeService knowledgeService, EmailService emailService)
        {
            _context = context;
            _knowledgeService = knowledgeService;
            _emailService = emailService;
        }

        // ==========================================================
        // 1. LISTAGEM COMPLETA DE USUÁRIOS
        // ==========================================================
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            var users = await _context.Users.ToListAsync();
            var relatorio = new List<object>();

            foreach (var user in users)
            {
                // Calcula qual agente o usuário mais usa
                var favAgentId = await _context.ChatMessages
                    .Where(m => m.UserId == user.Id)
                    .GroupBy(m => m.AgentId) 
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefaultAsync();

                relatorio.Add(new 
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.Plan,
                    user.Role,
                    
                    SubscriptionCycle = user.SubscriptionCycle ?? "Mensal",
                    CreatedAt = user.CreatedAt,
                    LastLogin = user.LastLogin,
                    
                    MostUsedAgent = favAgentId ?? "Nenhum",
                    UsedTokensCurrentMonth = user.UsedTokensCurrentMonth
                });
            }

            return Ok(relatorio.OrderByDescending(x => {
                dynamic d = x;
                return d.UsedTokensCurrentMonth;
            }));
        }

        // ==========================================================
        // 2. ATUALIZAR DADOS DO USUÁRIO
        // ==========================================================
        [HttpPost("update-user")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound(new { message = "Usuário não encontrado." });

            string planoAntigo = user.Plan;

            if (!string.IsNullOrEmpty(request.NewPlan)) 
                user.Plan = request.NewPlan;

            if (!string.IsNullOrEmpty(request.NewCycle)) 
                user.SubscriptionCycle = request.NewCycle;
            
            // Lógica de Admin
            if (request.NewPlan == "Admin") user.Role = "admin";
            else if (user.Role == "admin" && request.NewPlan != "Admin") user.Role = "user";

            if (request.ResetTokens) 
                user.UsedTokensCurrentMonth = 0;

            await _context.SaveChangesAsync();

            // --- INÍCIO DA ADIÇÃO: ALERTA DE ALTERAÇÃO DE PLANO ---
            if (planoAntigo != user.Plan)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var tipoMudanca = planoAntigo == "Free" || planoAntigo == "Iniciante" ? "Upgrade" : "Alteração de Plano";
                        var mensagem = $@"
                            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; border: 1px solid #e0e0e0; border-radius: 8px; max-width: 600px; margin: 0 auto;'>
                                <h2 style='color: #009966;'>{tipoMudanca} de Plano no Facility.IA</h2>
                                <p>O usuário <strong>{user.Name}</strong> ({user.Email}) atualizou sua assinatura.</p>
                                <ul style='list-style-type: none; padding: 0;'>
                                    <li><strong>Plano Anterior:</strong> {planoAntigo}</li>
                                    <li><strong>Novo Plano:</strong> {user.Plan}</li>
                                    <li><strong>Data e Hora:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</li>
                                </ul>
                                <p style='font-size: 12px; color: #888; margin-top: 20px; border-top: 1px solid #eee; padding-top: 10px;'>Este é um alerta automático do painel administrativo.</p>
                            </div>";

                        await _emailService.SendEmailAsync(
                            "klockk27@gmail.com",
                            "Aviso de Alteração de Plano - Facility.IA",
                            mensagem
                        );
                    }
                    catch { }
                });
            }
            // --- FIM DA ADIÇÃO ---

            return Ok(new { message = "Usuário atualizado com sucesso!" });
        }

        // ==========================================================
        // 3. ESTATÍSTICAS DO DASHBOARD
        // ==========================================================
        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
             var totalUsers = await _context.Users.CountAsync();
             var totalPro = await _context.Users.CountAsync(u => u.Plan == "Pro");
             
             var monthlyRevenue = await _context.Users
                .SumAsync(u => u.Plan == "Pro" ? 149.90 : u.Plan == "Plus" ? 59.90 : 0);

             return Ok(new { 
                 TotalUsers = totalUsers, 
                 TotalPro = totalPro, 
                 Revenue = monthlyRevenue 
             });
        }

        // ==========================================================
        // 4. GERENCIAMENTO DE ARQUIVOS
        // ==========================================================
        [HttpDelete("arquivo/{id}")]
        public async Task<IActionResult> ExcluirArquivo(int id)
        {
            bool sucesso = await _knowledgeService.ExcluirArquivo(id);
            if (sucesso) return Ok(new { message = "Arquivo excluído." });
            return BadRequest(new { message = "Erro ao excluir arquivo." });
        }

        [HttpPost("agente/prompt")]
        public async Task<IActionResult> AtualizarPrompt([FromBody] UpdatePromptRequest request)
        {
            var agent = await _context.Agents.FirstOrDefaultAsync(a => a.Name == request.AgentName);
            if (agent == null) return NotFound("Agente não encontrado.");

            agent.SystemInstruction = request.NewPrompt;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Prompt atualizado!" });
        }

        // ==========================================================
        // 5. EXCLUIR USUÁRIO (NOVO)
        // ==========================================================
        [HttpDelete("user/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "Usuário não encontrado." });

            _context.Users.Remove(user);
            
            try 
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Usuário excluído com sucesso." });
            }
            catch (System.Exception ex)
            {
                // Logar o erro 'ex' se necessário
                return BadRequest(new { message = "Erro ao excluir: O usuário possui dados vinculados." });
            }
        }

        // ==========================================================
        // DTOs (Data Transfer Objects) - Classes Auxiliares
        // ==========================================================
        public class UpdateUserRequest 
        { 
            public int UserId { get; set; } 
            public string NewPlan { get; set; } = string.Empty; 
            public string NewCycle { get; set; } = "Mensal"; 
            public bool ResetTokens { get; set; } = false;
        }

        public class UpdatePromptRequest 
        { 
            public string AgentName { get; set; } = string.Empty; 
            public string NewPrompt { get; set; } = string.Empty; 
        }

    } // Fim da classe AdminController
} // Fim do namespace