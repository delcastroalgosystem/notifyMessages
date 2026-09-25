using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Interfaces;

public interface IProviderFactory
{
    IEmailProvider GetEmailProvider(ProviderType type);
    ISmsProvider GetSmsProvider(ProviderType type);
    ICampaignProvider GetCampaignProvider(ProviderType type);
    ProviderConfig GetProviderConfig(int tenantId, ProviderType providerType);
}
