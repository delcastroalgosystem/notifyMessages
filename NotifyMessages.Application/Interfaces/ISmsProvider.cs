using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Interfaces;

public interface ISmsProvider
{
    ProviderType Type { get; }
    Task<ProviderResult> SendAsync(SmsMessage message, ProviderConfig config, CancellationToken ct = default);
}
