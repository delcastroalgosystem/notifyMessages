using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Interfaces;

public interface IEmailProvider
{
    ProviderType Type { get; }
    Task<ProviderResult> SendAsync(EmailMessage message, ProviderConfig config, CancellationToken ct = default);
}
