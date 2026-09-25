using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Infrastructure.Providers.Brevo.Models;

namespace NotifyMessages.Infrastructure.Providers.Brevo;

public class BrevoEmailProvider : IEmailProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BrevoEmailProvider> _logger;

    public ProviderType Type => ProviderType.Brevo;

    public BrevoEmailProvider(IHttpClientFactory httpClientFactory, ILogger<BrevoEmailProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ProviderResult> SendAsync(EmailMessage message, ProviderConfig config, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = config.BaseUrl ?? "https://api.brevo.com/v3";

            var request = new BrevoEmailRequest
            {
                Sender = new BrevoEmailContact { Email = message.From, Name = message.FromName },
                To = new List<BrevoEmailContact> { new() { Email = message.To, Name = message.ToName } },
                Subject = message.Subject,
                HtmlContent = message.HtmlBody,
                TextContent = message.PlainTextBody,
                Tags = string.IsNullOrEmpty(message.CorrelationId) ? null : new List<string> { message.CorrelationId }
            };

            if (message.ReplyTo != null)
            {
                request.ReplyTo = new BrevoEmailContact { Email = message.ReplyTo };
            }

            if (message.Attachments?.Any() == true)
            {
                request.Attachment = message.Attachments.Select(a => new BrevoAttachment
                {
                    Name = a.FileName,
                    Content = Convert.ToBase64String(a.Content)
                }).ToList();
            }

            var client = _httpClientFactory.CreateClient("Brevo");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("api-key", config.ApiKey);
            client.DefaultRequestHeaders.Add("accept", "application/json");

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{baseUrl}/smtp/email", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var brevoResponse = JsonSerializer.Deserialize<BrevoEmailResponse>(responseBody);
                _logger.LogInformation("Email enviado com sucesso via Brevo. MessageId: {MessageId}", brevoResponse?.MessageId);
                return ProviderResult.Ok(brevoResponse?.MessageId);
            }

            _logger.LogError("Erro ao enviar email via Brevo. Status: {Status}, Response: {Response}", response.StatusCode, responseBody);
            return ProviderResult.Fail(responseBody, response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao enviar email via Brevo");
            return ProviderResult.Fail(ex.Message);
        }
    }
}
