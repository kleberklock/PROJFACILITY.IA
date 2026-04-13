using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PROJFACILITY.IA.Data;
using PROJFACILITY.IA.Models;
using MercadoPago.Config;
using MercadoPago.Client.Preference;
using MercadoPago.Client.Payment;
using MercadoPago.Resource.Preference;
using MercadoPago.Resource.Payment;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Text.Json;

namespace PROJFACILITY.IA.Controllers
{
    [Route("api/payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(AppDbContext context, IConfiguration configuration, ILogger<PaymentController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;

            var accessToken = _configuration["MercadoPago:AccessToken"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                MercadoPago.Config.MercadoPagoConfig.AccessToken = accessToken;
            }
        }

        // 1. ENDPOINT PARA CRIAR A SESSÃO DE CHECKOUT (Restrito a usuários logados)
        [Authorize]
        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CriarSessaoCheckout([FromBody] CheckoutRequest request)
        {
            try
            {
                // Extrai o ID do utilizador autenticado a partir do token JWT
                var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                    return Unauthorized(new { message = "Usuário não autenticado." });

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound(new { message = "Usuário não encontrado." });

                var domain = _configuration["App:Domain"] ?? "http://localhost:5217";

                decimal basePrice = request.Plan.ToLower() == "pro" ? 149.90m : 59.90m;
                decimal finalPrice = basePrice;
                string descCiclo = "Mensal";

                if (request.Cycle == "quarterly")
                {
                    finalPrice = (basePrice * 3) * 0.9m;
                    descCiclo = "Trimestral";
                }
                else if (request.Cycle == "annual")
                {
                    finalPrice = (basePrice * 12) * 0.8m;
                    descCiclo = "Anual";
                }

                var preferenceRequest = new PreferenceRequest
                {
                    Items = new List<PreferenceItemRequest>
                    {
                        new PreferenceItemRequest
                        {
                            Title = $"Facility.IA - Plano {request.Plan} ({descCiclo})",
                            Description = "Assinatura Premium PROJFACILITY.IA",
                            Quantity = 1,
                            CurrencyId = "BRL",
                            UnitPrice = finalPrice,
                        }
                    },
                    BackUrls = new PreferenceBackUrlsRequest
                    {
                        Success = $"{domain}/sucesso.html",
                        Failure = $"{domain}/checkout.html?payment=canceled",
                        Pending = $"{domain}/checkout.html?payment=pending"
                    },
                    AutoReturn = "approved",
                    PaymentMethods = new PreferencePaymentMethodsRequest
                    {
                        ExcludedPaymentTypes = new List<PreferencePaymentTypeRequest>
                        {
                            new PreferencePaymentTypeRequest { Id = "ticket" } // Exclui boleto, aceita apenas Cartão e PIX
                        }
                    },
                    ExternalReference = userId.ToString(), // Super importante para identificar quem pagou no webhook
                    NotificationUrl = $"{domain}/api/payment/webhook" // MP enviará POST pra cá intermitentemente
                };

                var client = new PreferenceClient();
                Preference preference = await client.CreateAsync(preferenceRequest);

                // Retorna a URL (InitPoint) para o front-end redirecionar o utilizador para a página do Mercado Pago
                return Ok(new { url = preference.InitPoint });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro interno ao criar sessão do Mercado Pago");
                return StatusCode(500, new { message = "Ocorreu um erro interno ao processar o checkout." });
            }
        }

        // 2. ENDPOINT DE WEBHOOK DO MERCADO PAGO 
        [HttpPost("webhook")]
        public async Task<IActionResult> MercadoPagoWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                _logger.LogInformation($"[MP Webhook] JSON recebido: {json}");

                // Se houver uma secret de assinatura configurada no seu appsettings,
                // você também pode ler Request.Headers["x-signature"] para validar a origem via HMAC (código não ilustrado aqui).
                
                if (string.IsNullOrEmpty(json)) return BadRequest();

                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // O Mercado Pago pode mandar eventos de 'payment' nestes dois formatos principais
                bool isPaymentEvent = (root.TryGetProperty("type", out var typeElement) && typeElement.GetString() == "payment") ||
                                      (root.TryGetProperty("action", out var actionElement) && actionElement.GetString() != null && actionElement.GetString()!.StartsWith("payment"));

                if (isPaymentEvent)
                {
                    string? paymentIdStr = null;
                    if (root.TryGetProperty("data", out var dataElement) && dataElement.TryGetProperty("id", out var idElement))
                    {
                        paymentIdStr = idElement.ToString();
                    }

                    if (!string.IsNullOrEmpty(paymentIdStr) && long.TryParse(paymentIdStr, out long paymentId))
                    {
                        // Consulta a API do MP usando o PaymentClient para verificar o status real
                        // Isso previne que chamadas fakes com payloads de "approved" invadam o banco
                        var client = new PaymentClient();
                        Payment payment = await client.GetAsync(paymentId);

                        if (payment.Status == "approved")
                        {
                            // A ExternalReference carrega o ID do usuário que injetamos na criação
                            if (!string.IsNullOrEmpty(payment.ExternalReference) && int.TryParse(payment.ExternalReference, out int userId))
                            {
                                var user = await _context.Users.FindAsync(userId);
                                if (user != null)
                                {
                                    // Determina o plano que ele pagou baseado no Title gerado na payment.Description
                                    // ou fazemos um upgrade padrão:
                                    string planoComprado = "Pro";
                                    if (payment.Description != null && payment.Description.Contains("Plus")) planoComprado = "Plus";
                                    
                                    string cicloComprado = "Mensal";
                                    if (payment.Description != null)
                                    {
                                        if (payment.Description.Contains("Anual")) cicloComprado = "Anual";
                                        if (payment.Description.Contains("Trimestral")) cicloComprado = "Trimestral";
                                    }

                                    user.Plan = planoComprado; 
                                    user.IsActive = true;
                                    user.SubscriptionCycle = cicloComprado;

                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation($"[WEBHOOK] Pagamento Aprovado. Assinatura ativada para UserId: {userId} - Plano: {planoComprado}");
                                }
                            }
                        }
                    }
                }

                // O Mercado Pago ESPERA um StatusCode 200/201 para saber que você recebeu a msg. Senão, tenta reenviar.
                return Ok(); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro interno no processamento do Webhook do Mercado Pago");
                return StatusCode(500);
            }
        }
    }

    public class CheckoutRequest
    {
        public string Plan { get; set; } = "Plus";
        public string Cycle { get; set; } = "monthly";
    }
}