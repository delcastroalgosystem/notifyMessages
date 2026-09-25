using NotifyMessages.Application.DTOs;

namespace NotifyMessages.Application.Interfaces;

public interface IWebhookEventService
{
    Task ProcessEventAsync(WebhookEventDto webhookEvent, CancellationToken ct = default);
}
