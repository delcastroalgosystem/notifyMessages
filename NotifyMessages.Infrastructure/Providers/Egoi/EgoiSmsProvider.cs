using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Infrastructure.Providers.Egoi.Models;

namespace NotifyMessages.Infrastructure.Providers.Egoi;

public class EgoiSmsProvider : ISmsProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EgoiSmsProvider> _logger;

    public ProviderType Type => ProviderType.Egoi;

    public EgoiSmsProvider(IHttpClientFactory httpClientFactory, ILogger<EgoiSmsProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ProviderResult> SendAsync(SmsMessage message, ProviderConfig config, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = config.BaseUrl ?? "https://slingshot.egoiapp.com/api/v2";
            var request = new EgoiSmsRequest
            {
                To = message.To,
                From = message.From,
                TextBody = message.TextBody,
                Encoding = "utf-8",
                MaxCount = message.MaxParts,
                Priority = "normal",
                Registered = false,
                Group = "default",
                ScheduleTo = message.ScheduleTo
            };

            var client = _httpClientFactory.CreateClient("Egoi");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("ApiKey", config.ApiKey);

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{baseUrl}/sms/messages/action/send/single", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var egoiResponse = JsonSerializer.Deserialize<EgoiResponse>(responseBody);
                _logger.LogInformation("SMS enviado com sucesso via E-goi. ExternalId: {ExternalId}", egoiResponse?.Id);
                return ProviderResult.Ok(egoiResponse?.Id);
            }

            _logger.LogError("Erro ao enviar SMS via E-goi. Status: {Status}, Response: {Response}", response.StatusCode, responseBody);
            return ProviderResult.Fail(responseBody, response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao enviar SMS via E-goi");
            return ProviderResult.Fail(ex.Message);
        }
    }
}
