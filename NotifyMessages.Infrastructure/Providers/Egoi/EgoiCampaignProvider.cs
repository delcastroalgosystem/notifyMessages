using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Infrastructure.Providers.Egoi.Models;

namespace NotifyMessages.Infrastructure.Providers.Egoi;

public class EgoiCampaignProvider : ICampaignProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EgoiCampaignProvider> _logger;

    public ProviderType Type => ProviderType.EgoiCampaign;

    public EgoiCampaignProvider(IHttpClientFactory httpClientFactory, ILogger<EgoiCampaignProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ProviderResult> CreateAndSendCampaignAsync(CampaignRequest request, ProviderConfig config, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = config.BaseUrl ?? "https://api.egoi.io/v3";
            var client = _httpClientFactory.CreateClient("EgoiCampaign");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("ApiKey", config.ApiKey);

            var campaignRequest = new EgoiCampaignRequest
            {
                Title = request.CampaignName,
                Subject = request.Subject,
                SenderName = request.FromName,
                SenderEmail = request.FromEmail,
                ListId = request.ListId,
                HtmlBody = request.HtmlBody,
                OpenTracking = request.OpenTracking,
                ClickTracking = request.ClickTracking
            };

            var jsonContent = JsonSerializer.Serialize(campaignRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var createResponse = await client.PostAsync($"{baseUrl}/campaigns/email", content, ct);
            var createResponseBody = await createResponse.Content.ReadAsStringAsync(ct);

            if (!createResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao criar campanha E-goi. Status: {Status}, Response: {Response}", createResponse.StatusCode, createResponseBody);
                return ProviderResult.Fail(createResponseBody, createResponse.StatusCode.ToString());
            }

            var campaignResponse = JsonSerializer.Deserialize<EgoiCampaignResponse>(createResponseBody);
            var campaignId = campaignResponse?.Id;

            if (campaignId == null)
            {
                return ProviderResult.Fail("Campaign ID não retornado");
            }

            var sendResponse = await client.PostAsync($"{baseUrl}/campaigns/email/{campaignId}/send", null, ct);
            var sendResponseBody = await sendResponse.Content.ReadAsStringAsync(ct);

            if (sendResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Campanha E-goi enviada com sucesso. CampaignId: {CampaignId}", campaignId);
                return ProviderResult.Ok(campaignId.ToString());
            }

            _logger.LogError("Erro ao enviar campanha E-goi. Status: {Status}, Response: {Response}", sendResponse.StatusCode, sendResponseBody);
            return ProviderResult.Fail(sendResponseBody, sendResponse.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao criar/enviar campanha E-goi");
            return ProviderResult.Fail(ex.Message);
        }
    }
}
