using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Interfaces;

public interface ICampaignProvider
{
    ProviderType Type { get; }
    Task<ProviderResult> CreateAndSendCampaignAsync(CampaignRequest request, ProviderConfig config, CancellationToken ct = default);
}
