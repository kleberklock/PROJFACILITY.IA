using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using PROJFACILITY.IA.Models;

namespace PROJFACILITY.IA.Services
{
    public class EmailService
    {
        private readonly EmailSettingsOptions _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettingsOptions> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string emailDestino, string assunto, string mensagemHtml)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Facility.IA", _emailSettings.Remetente));
            message.To.Add(new MailboxAddress("", emailDestino));
            message.Subject = assunto;

            var bodyBuilder = new BodyBuilder { HtmlBody = mensagemHtml };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                // Limpeza da senha para evitar espaços acidentais que o Gmail não aceita
                var senha = _emailSettings.SenhaApp?.Replace(" ", "");

                if (string.IsNullOrEmpty(_emailSettings.Remetente) || string.IsNullOrEmpty(senha))
                {
                    _logger.LogError("Configurações de e-mail incompletas no appsettings.json.");
                    return false;
                }

                _logger.LogInformation("Iniciando envio de e-mail para {Email} via {SmtpServer}:{Porta}", emailDestino, _emailSettings.SmtpServer, _emailSettings.Porta);
                
                // Conectar usando StartTls (exigência do Gmail para porta 587)
                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Porta, SecureSocketOptions.StartTls);
                
                // Autenticar
                await client.AuthenticateAsync(_emailSettings.Remetente, senha);
                
                // Enviar
                await client.SendAsync(message);
                
                // Desconectar
                await client.DisconnectAsync(true);

                _logger.LogInformation("E-mail enviado com sucesso para {Email}", emailDestino);
                return true;
            }
            catch (SmtpCommandException ex)
            {
                _logger.LogError(ex, "Erro de comando SMTP ao enviar para {Email}. Código: {StatusCode}, Resposta: {Message}", emailDestino, ex.StatusCode, ex.Message);
                return false;
            }
            catch (SmtpProtocolException ex)
            {
                _logger.LogError(ex, "Erro de protocolo SMTP ao enviar para {Email}: {Message}", emailDestino, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao enviar e-mail para {Email}: {Message}", emailDestino, ex.Message);
                return false;
            }
        }
    }
}