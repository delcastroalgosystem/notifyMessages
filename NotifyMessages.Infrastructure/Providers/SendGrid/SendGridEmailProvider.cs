using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Application.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NotifyMessages.Infrastructure.Providers.SendGrid;

public class SendGridEmailProvider : IEmailProvider
{
    private readonly ILogger<SendGridEmailProvider> _logger;

    public ProviderType Type => ProviderType.SendGrid;

    public SendGridEmailProvider(ILogger<SendGridEmailProvider> logger)
    {
        _logger = logger;
    }

    public async Task<ProviderResult> SendAsync(EmailMessage message, ProviderConfig config, CancellationToken ct = default)
    {
        try
        {
            var client = new SendGridClient(config.ApiKey);
            var from = new EmailAddress(message.From, message.FromName);
            var to = new EmailAddress(message.To, message.ToName);

            var msg = MailHelper.CreateSingleEmail(
                from,
                to,
                message.Subject,
                message.PlainTextBody ?? string.Empty,
                message.HtmlBody
            );

            if (message.ReplyTo != null)
            {
                msg.ReplyTo = new EmailAddress(message.ReplyTo);
            }

            if (message.Attachments?.Any() == true)
            {
                foreach (var attachment in message.Attachments)
                {
                    msg.AddAttachment(
                        attachment.FileName,
                        Convert.ToBase64String(attachment.Content),
                        attachment.ContentType
                    );
                }
            }

            msg.SetClickTracking(message.ClickTracking, message.ClickTracking);
            msg.SetOpenTracking(message.OpenTracking);

            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                msg.AddCustomArg("dispatch_id", message.CorrelationId);
            }

            var response = await client.SendEmailAsync(msg, ct);

            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers?.GetValues("X-Message-Id")?.FirstOrDefault();
                _logger.LogInformation("Email enviado com sucesso via SendGrid. MessageId: {MessageId}", messageId);
                return ProviderResult.Ok(messageId);
            }

            var responseBody = await response.Body.ReadAsStringAsync(ct);
            _logger.LogError("Erro ao enviar email via SendGrid. Status: {Status}, Response: {Response}", response.StatusCode, responseBody);
            return ProviderResult.Fail(responseBody, response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao enviar email via SendGrid");
            return ProviderResult.Fail(ex.Message);
        }
    }
}
