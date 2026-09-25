using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Infrastructure.Providers.Brevo.Models;

namespace NotifyMessages.Infrastructure.Providers.Brevo;

public class BrevoSmsProvider : ISmsProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BrevoSmsProvider> _logger;

    public ProviderType Type => ProviderType.BrevoSms;

    public BrevoSmsProvider(IHttpClientFactory httpClientFactory, ILogger<BrevoSmsProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ProviderResult> SendAsync(SmsMessage message, ProviderConfig config, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = config.BaseUrl ?? "https://api.brevo.com/v3";

            var request = new BrevoSmsRequest
            {
                Sender = message.From,
                Recipient = message.To,
                Content = message.TextBody,
                Type = "transactional"
            };

            var client = _httpClientFactory.CreateClient("Brevo");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("api-key", config.ApiKey);
            client.DefaultRequestHeaders.Add("accept", "application/json");

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{baseUrl}/transactionalSMS/send", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var brevoResponse = JsonSerializer.Deserialize<BrevoSmsResponse>(responseBody);
                _logger.LogInformation("SMS enviado com sucesso via Brevo. MessageId: {MessageId}", brevoResponse?.MessageId);
                return ProviderResult.Ok(brevoResponse?.MessageId?.ToString());
            }

            _logger.LogError("Erro ao enviar SMS via Brevo. Status: {Status}, Response: {Response}", response.StatusCode, responseBody);
            return ProviderResult.Fail(responseBody, response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao enviar SMS via Brevo");
            return ProviderResult.Fail(ex.Message);
        }
    }
}
