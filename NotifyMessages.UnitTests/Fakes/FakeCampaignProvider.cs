using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Fakes;

internal class FakeCampaignProvider : ICampaignProvider
{
    public ProviderType Type { get; }
    public Func<CampaignRequest, ProviderConfig, ProviderResult> Handler { get; set; } = (_, _) => ProviderResult.Ok("fake-campaign-id");
    public int CallCount { get; private set; }

    public FakeCampaignProvider(ProviderType type)
    {
        Type = type;
    }

    public Task<ProviderResult> CreateAndSendCampaignAsync(CampaignRequest request, ProviderConfig config, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(Handler(request, config));
    }
}
