using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Fakes;

internal class FakeSmsProvider : ISmsProvider
{
    public ProviderType Type { get; }
    public Func<SmsMessage, ProviderConfig, ProviderResult> Handler { get; set; } = (_, _) => ProviderResult.Ok("fake-external-id");
    public int CallCount { get; private set; }

    public FakeSmsProvider(ProviderType type)
    {
        Type = type;
    }

    public Task<ProviderResult> SendAsync(SmsMessage message, ProviderConfig config, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(Handler(message, config));
    }
}
