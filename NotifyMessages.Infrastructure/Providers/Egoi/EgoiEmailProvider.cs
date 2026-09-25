using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Infrastructure.Providers.Egoi.Models;

namespace NotifyMessages.Infrastructure.Providers.Egoi;

public class EgoiEmailProvider : IEmailProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EgoiEmailProvider> _logger;

    public ProviderType Type => ProviderType.Egoi;

    public EgoiEmailProvider(IHttpClientFactory httpClientFactory, ILogger<EgoiEmailProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ProviderResult> SendAsync(EmailMessage message, ProviderConfig config, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = config.BaseUrl ?? "https://slingshot.egoiapp.com/api/v2";
            var request = new EgoiEmailRequest
            {
                Domain = config.Domain ?? string.Empty,
                SenderId = message.From,
                SenderName = message.FromName,
                To = new List<string> { message.To },
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                OpenTracking = message.OpenTracking,
                ClickTracking = message.ClickTracking,
                Priority = "non-urgent",
                Registered = false
            };

            if (message.Attachments?.Any() == true)
            {
                request.AttachedFiles = message.Attachments.Select(a => new EgoiAttachment
                {
                    Name = a.FileName,
                    Content = Convert.ToBase64String(a.Content),
                    ContentType = a.ContentType
                }).ToList();
            }

            var client = _httpClientFactory.CreateClient("Egoi");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("ApiKey", config.ApiKey);

            var jsonContent = JsonSerializer.Serialize(new[] { request });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{baseUrl}/email/messages/action/send", content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var egoiResponse = JsonSerializer.Deserialize<List<EgoiResponse>>(responseBody);
                var externalId = egoiResponse?.FirstOrDefault()?.Id;
                _logger.LogInformation("Email enviado com sucesso via E-goi. ExternalId: {ExternalId}", externalId);
                return ProviderResult.Ok(externalId);
            }

            _logger.LogError("Erro ao enviar email via E-goi. Status: {Status}, Response: {Response}", response.StatusCode, responseBody);
            return ProviderResult.Fail(responseBody, response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao enviar email via E-goi");
            return ProviderResult.Fail(ex.Message);
        }
    }
}
