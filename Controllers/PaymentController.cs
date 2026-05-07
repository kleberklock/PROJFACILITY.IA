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
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

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

        // ─────────────────────────────────────────────────────────────────────
        // 1. ENDPOINT PARA CRIAR A SESSÃO DE CHECKOUT (Restrito a usuários logados)
        // ─────────────────────────────────────────────────────────────────────
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

                // ── PREÇOS OFICIAIS (sincronizados com o front-end) ──────────
                // Plus: R$ 49,90/mês | Pro: R$ 99,90/mês
                decimal basePrice = request.Plan.ToLower() == "pro" ? 99.90m : 49.90m;

                decimal finalPrice = basePrice;
                string descCiclo = "Mensal";

                // Desconto de 10% no trimestral (paga 3 meses de uma vez)
                if (request.Cycle == "quarterly")
                {
                    finalPrice = (basePrice * 3) * 0.9m;
                    descCiclo = "Trimestral";
                }
                // Desconto de 20% no anual (paga 12 meses de uma vez)
                else if (request.Cycle == "annual")
                {
                    finalPrice = (basePrice * 12) * 0.8m;
                    descCiclo = "Anual";
                }

                // ── EXTERNAL REFERENCE ESTRUTURADA ──────────────────────────
                // Formato: "{userId}|{plano}|{ciclo}"
                // Isso garante que o webhook identifique o plano sem depender da descrição textual.
                string externalReference = $"{userId}|{request.Plan}|{request.Cycle}";

                var preferenceRequest = new PreferenceRequest
                {
                    Items = new List<PreferenceItemRequest>
                    {
                        new PreferenceItemRequest
                        {
                            Title = $"Facility.IA - Plano {request.Plan} ({descCiclo})",
                            Description = "Assinatura Premium FACILITY.IA",
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
                            new PreferencePaymentTypeRequest { Id = "ticket" } // Exclui boleto; aceita Cartão e PIX
                        }
                    },
                    // ExternalReference estruturada para identificação confiável no webhook
                    ExternalReference = externalReference,
                    NotificationUrl = $"{domain}/api/payment/webhook"
                };

                var client = new PreferenceClient();
                Preference preference = await client.CreateAsync(preferenceRequest);

                _logger.LogInformation("[CHECKOUT] Preferência criada. UserId={UserId} | Plano={Plan} | Ciclo={Cycle} | Valor=R${Price:F2}",
                    userId, request.Plan, request.Cycle, finalPrice);

                // Retorna a URL (InitPoint) para o front-end redirecionar o utilizador
                return Ok(new { url = preference.InitPoint });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro interno ao criar sessão do Mercado Pago");
                return StatusCode(500, new { message = "Ocorreu um erro interno ao processar o checkout." });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. ENDPOINT DE WEBHOOK DO MERCADO PAGO
        // ─────────────────────────────────────────────────────────────────────
        [HttpPost("webhook")]
        public async Task<IActionResult> MercadoPagoWebhook()
        {
            // Lê o corpo bruto da requisição para validação HMAC e parsing
            string json;
            using (var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8))
            {
                json = await reader.ReadToEndAsync();
            }

            _logger.LogInformation("[MP Webhook] JSON recebido: {Json}", json);

            if (string.IsNullOrEmpty(json))
                return BadRequest(new { message = "Payload vazio." });

            // ── VALIDAÇÃO DE ASSINATURA HMAC SHA256 (Segurança Oficial MP) ─────
            var webhookSecret = _configuration["MercadoPago:WebhookSecret"];
            if (!string.IsNullOrEmpty(webhookSecret))
            {
                // O MP envia o cabeçalho x-signature no formato: "ts=<timestamp>,v1=<hash>"
                var signatureHeader = Request.Headers["x-signature"].ToString();
                var requestId       = Request.Headers["x-request-id"].ToString();

                if (string.IsNullOrEmpty(signatureHeader))
                {
                    _logger.LogWarning("[MP Webhook] Requisição rejeitada: cabeçalho x-signature ausente.");
                    return Unauthorized(new { message = "Assinatura ausente." });
                }

                // Extrai o timestamp e o hash do cabeçalho
                string? ts = null;
                string? v1 = null;
                foreach (var part in signatureHeader.Split(','))
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length == 2)
                    {
                        if (kv[0].Trim() == "ts") ts = kv[1].Trim();
                        if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
                    }
                }

                if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1))
                {
                    _logger.LogWarning("[MP Webhook] Cabeçalho x-signature malformado: {Header}", signatureHeader);
                    return BadRequest(new { message = "Formato de assinatura inválido." });
                }

                // Monta a string de manifesto conforme a documentação oficial do Mercado Pago:
                // "id:<data.id>;request-id:<x-request-id>;ts:<ts>;"
                string? dataId = null;
                try
                {
                    using JsonDocument docTemp = JsonDocument.Parse(json);
                    if (docTemp.RootElement.TryGetProperty("data", out var dataEl) &&
                        dataEl.TryGetProperty("id", out var idEl))
                    {
                        dataId = idEl.ToString();
                    }
                }
                catch { /* ignora erro de parse no pré-check */ }

                var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
                var keyBytes = Encoding.UTF8.GetBytes(webhookSecret);
                var dataBytes = Encoding.UTF8.GetBytes(manifest);

                using var hmac = new HMACSHA256(keyBytes);
                var computedHash = BitConverter.ToString(hmac.ComputeHash(dataBytes))
                                               .Replace("-", "")
                                               .ToLowerInvariant();

                if (!string.Equals(computedHash, v1, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[MP Webhook] Assinatura HMAC inválida. Esperado={Expected} | Recebido={Received}", computedHash, v1);
                    return Unauthorized(new { message = "Assinatura inválida." });
                }

                _logger.LogInformation("[MP Webhook] Assinatura HMAC validada com sucesso.");
            }
            else
            {
                _logger.LogWarning("[MP Webhook] WebhookSecret não configurado — validação HMAC ignorada.");
            }

            // ── PROCESSAMENTO DO EVENTO ──────────────────────────────────────
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                bool isPaymentEvent =
                    (root.TryGetProperty("type", out var typeElement) && typeElement.GetString() == "payment") ||
                    (root.TryGetProperty("action", out var actionElement) &&
                     actionElement.GetString() is string action && action.StartsWith("payment"));

                if (isPaymentEvent)
                {
                    string? paymentIdStr = null;
                    if (root.TryGetProperty("data", out var dataElement) &&
                        dataElement.TryGetProperty("id", out var idElement))
                    {
                        paymentIdStr = idElement.ToString();
                    }

                    if (!string.IsNullOrEmpty(paymentIdStr) && long.TryParse(paymentIdStr, out long paymentId))
                    {
                        // Consulta o status REAL na API do MP (previne payloads falsos com status "approved")
                        var client  = new PaymentClient();
                        Payment payment = await client.GetAsync(paymentId);

                        if (payment.Status == "approved")
                        {
                            // ── PARSE SEGURO DA EXTERNAL REFERENCE ──────────
                            // Formato esperado: "{userId}|{plano}|{ciclo}"
                            var externalRef = payment.ExternalReference ?? string.Empty;
                            var parts = externalRef.Split('|');

                            if (parts.Length == 3 && int.TryParse(parts[0], out int userId))
                            {
                                string planoComprado = parts[1]; // "Plus" ou "Pro"
                                string cicloRaw      = parts[2]; // "monthly", "quarterly" ou "annual"

                                // Normaliza o ciclo para o valor em português armazenado no banco
                                string cicloComprado = cicloRaw switch
                                {
                                    "quarterly" => "Trimestral",
                                    "annual"    => "Anual",
                                    _           => "Mensal"
                                };

                                var user = await _context.Users.FindAsync(userId);
                                if (user != null)
                                {
                                    user.Plan             = planoComprado;
                                    user.IsActive         = true;
                                    user.SubscriptionCycle = cicloComprado;

                                    await _context.SaveChangesAsync();
                                    _logger.LogInformation(
                                        "[WEBHOOK] Pagamento aprovado. UserId={UserId} | Plano={Plan} | Ciclo={Cycle} | PaymentId={PaymentId}",
                                        userId, planoComprado, cicloComprado, paymentId);
                                }
                                else
                                {
                                    _logger.LogWarning("[WEBHOOK] Pagamento aprovado mas UserId={UserId} não encontrado no banco.", userId);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("[WEBHOOK] ExternalReference malformada ou inválida: '{ExternalRef}'", externalRef);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("[WEBHOOK] Pagamento PaymentId={PaymentId} com status '{Status}' — ignorado.", paymentId, payment.Status);
                        }
                    }
                }

                // O Mercado Pago ESPERA um 200/201 para confirmar o recebimento, senão tentará reenviar.
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
        public string Plan  { get; set; } = "Plus";
        public string Cycle { get; set; } = "monthly";
    }
}