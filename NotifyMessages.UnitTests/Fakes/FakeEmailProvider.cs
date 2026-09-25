using NotifyMessages.Application.DTOs.Providers;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Fakes;

internal class FakeEmailProvider : IEmailProvider
{
    public ProviderType Type { get; }
    public Func<EmailMessage, ProviderConfig, ProviderResult> Handler { get; set; } = (_, _) => ProviderResult.Ok("fake-external-id");
    private int _callCount;
    public int CallCount => _callCount;

    public FakeEmailProvider(ProviderType type)
    {
        Type = type;
    }

    public Task<ProviderResult> SendAsync(EmailMessage message, ProviderConfig config, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _callCount); // o Worker chama em paralelo
        return Task.FromResult(Handler(message, config));
    }
}
