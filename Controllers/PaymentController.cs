using Microsoft.AspNetCore.Mvc;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using PROJFACILITY.IA.Services;
using Stripe;
using Stripe.Checkout;
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace PROJFACILITY.IA.Controllers
{
    [Route("api/payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;
        private readonly EmailService _emailService;

        public PaymentController(AppDbContext context, IConfiguration configuration, ILogger<PaymentController> logger, EmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutRequest request)
        {
            try 
            {
                // 1. VERIFICAÇÃO DE SEGURANÇA (Chave Stripe)
                var apiKey = _configuration["Stripe:SecretKey"];
                if (string.IsNullOrEmpty(apiKey) || !apiKey.StartsWith("sk_"))
                {
                    return BadRequest(new { message = "CONFIGURAÇÃO INVÁLIDA: Chave do Stripe não encontrada ou incorreta no appsettings.json." });
                }
                StripeConfiguration.ApiKey = apiKey;

                // 2. Validação do Usuário
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null) 
                {
                    // Retorna 404 para o front-end forçar logout
                    return NotFound(new { message = "Usuário não encontrado." });
                }

                var domain = _configuration["App:Domain"] ?? "http://localhost:5217";

                // 3. Cálculo do Preço (Centavos)
                long basePrice = request.Plan.ToLower() == "pro" ? 14990 : 5990;
                long finalPrice = basePrice;
                
                string interval = "month";
                int intervalCount = 1;
                string descCiclo = "Mensal";

                if (request.Cycle == "quarterly") 
                {
                    finalPrice = (long)((basePrice * 3) * 0.9);
                    interval = "month";
                    intervalCount = 3;
                    descCiclo = "Trimestral";
                }
                else if (request.Cycle == "annual") 
                {
                    finalPrice = (long)((basePrice * 12) * 0.8);
                    interval = "year";
                    intervalCount = 1;
                    descCiclo = "Anual";
                }

                // 4. Criação da Sessão
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmount = finalPrice,
                                Currency = "brl",
                                Recurring = new SessionLineItemPriceDataRecurringOptions
                                {
                                    Interval = interval,
                                    IntervalCount = intervalCount
                                },
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"Facility.IA - Plano {request.Plan} ({descCiclo})",
                                    Description = "Assinatura Premium"
                                },
                            },
                            Quantity = 1,
                        },
                    },
                    Mode = "subscription",
                    SuccessUrl = $"{domain}/sucesso.html?plan={request.Plan}",
                    CancelUrl = $"{domain}/checkout.html?payment=canceled",
                    Metadata = new Dictionary<string, string>
                    {
                        { "userId", user.Id.ToString() },
                        { "planName", request.Plan },
                        { "cycle", request.Cycle }
                    }
                };

                var service = new SessionService();
                Session session = await service.CreateAsync(options);

                return Ok(new { url = session.Url });
            }
            catch (StripeException e)
            {
                _logger.LogError(e, "Erro na API do Stripe");
                // Retorna o erro exato do Stripe para o Front-end
                return BadRequest(new { message = $"Erro Stripe: {e.StripeError.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro Interno no Pagamento");
                return StatusCode(500, new { message = $"Erro Interno: {ex.Message}" });
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json, Request.Headers["Stripe-Signature"], _configuration["Stripe:WebhookSecret"]
                );

                // CORREÇÃO: Usando string direta para evitar erros de compilação
                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Session;

                    if (session?.Metadata != null &&
                        session.Metadata.TryGetValue("userId", out string? userIdStr))
                    {
                        if (int.TryParse(userIdStr, out int userId))
                        {
                            string planName = session.Metadata.GetValueOrDefault("planName", "Plus");
                            string cycle = session.Metadata.GetValueOrDefault("cycle", "Mensal");

                            await AtivarPlanoUsuario(userId, planName, cycle);
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook Falhou");
                return BadRequest();
            }
        }

        private async Task AtivarPlanoUsuario(int userId, string planName, string cycle)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                string planoAntigo = user.Plan;

                user.Plan = planName;
                user.SubscriptionCycle = cycle;
                user.IsActive = true; 
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Usuário {userId} ativado: {planName}");

                // --- INÍCIO DA ADIÇÃO: ALERTA DE ALTERAÇÃO DE PLANO STRIPE ---
                if (planoAntigo != planName)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var tipoMudanca = planoAntigo == "Free" || planoAntigo == "Iniciante" ? "Upgrade" : "Alteração de Plano";
                            var mensagem = $@"
                                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; border: 1px solid #e0e0e0; border-radius: 8px; max-width: 600px; margin: 0 auto;'>
                                    <h2 style='color: #009966;'>{tipoMudanca} de Plano no Facility.IA</h2>
                                    <p>O usuário <strong>{user.Name}</strong> ({user.Email}) atualizou sua assinatura via Stripe.</p>
                                    <ul style='list-style-type: none; padding: 0;'>
                                        <li><strong>Plano Anterior:</strong> {planoAntigo}</li>
                                        <li><strong>Novo Plano:</strong> {planName}</li>
                                        <li><strong>Novo Ciclo:</strong> {cycle}</li>
                                        <li><strong>Data e Hora:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</li>
                                    </ul>
                                    <p style='font-size: 12px; color: #888; margin-top: 20px; border-top: 1px solid #eee; padding-top: 10px;'>Este é um alerta automático do sistema de pagamentos da plataforma.</p>
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
            }
        }
    }

    public class CheckoutRequest
    {
        public int UserId { get; set; }
        public string Plan { get; set; } = "Plus";
        public string Cycle { get; set; } = "monthly";
    }
}