using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Services;

public class WebhookEventService : IWebhookEventService
{
    private static readonly Dictionary<DispatchStatus, int> ProgressOrder = new()
    {
        [DispatchStatus.Queued] = 0,
        [DispatchStatus.Processing] = 1,
        [DispatchStatus.Sent] = 2,
        [DispatchStatus.Delivered] = 3,
        [DispatchStatus.Read] = 4
    };

    private readonly IAppDbContext _context;
    private readonly ILogger<WebhookEventService> _logger;

    public WebhookEventService(IAppDbContext context, ILogger<WebhookEventService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ProcessEventAsync(WebhookEventDto webhookEvent, CancellationToken ct = default)
    {
        MessageDispatch? dispatch = null;

        if (webhookEvent.DispatchId.HasValue)
        {
            dispatch = await _context.MessageDispatches
                .FirstOrDefaultAsync(d => d.Id == webhookEvent.DispatchId.Value, ct);
        }

        if (dispatch == null && !string.IsNullOrEmpty(webhookEvent.ExternalId))
        {
            dispatch = await _context.MessageDispatches
                .FirstOrDefaultAsync(d => d.ExternalId == webhookEvent.ExternalId, ct);
        }

        if (dispatch == null)
        {
            _logger.LogWarning(
                "Evento de webhook sem correspondência a nenhum MessageDispatch. EventType={EventType}, DispatchId={DispatchId}, ExternalId={ExternalId}",
                webhookEvent.EventType, webhookEvent.DispatchId, webhookEvent.ExternalId);
            return;
        }

        _context.MessageDispatchEvents.Add(new MessageDispatchEvent
        {
            DispatchId = dispatch.Id,
            EventType = webhookEvent.EventType,
            EventDate = webhookEvent.EventDate,
            ProviderResponse = webhookEvent.RawPayload
        });

        if (webhookEvent.MappedStatus.HasValue && ShouldAdvanceStatus(dispatch.CurrentStatus, webhookEvent.MappedStatus.Value))
        {
            dispatch.CurrentStatus = webhookEvent.MappedStatus.Value;

            if (webhookEvent.MappedStatus.Value == DispatchStatus.Delivered && dispatch.ProcessedAt == null)
            {
                dispatch.ProcessedAt = webhookEvent.EventDate;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    private static bool ShouldAdvanceStatus(DispatchStatus current, DispatchStatus incoming)
    {
        var isTerminal = current is DispatchStatus.Bounced or DispatchStatus.Failed or DispatchStatus.Canceled;
        if (isTerminal)
        {
            return false;
        }

        var incomingIsTerminal = incoming is DispatchStatus.Bounced or DispatchStatus.Failed or DispatchStatus.Canceled;
        if (incomingIsTerminal)
        {
            return true;
        }

        return ProgressOrder.TryGetValue(incoming, out var incomingRank)
            && ProgressOrder.TryGetValue(current, out var currentRank)
            && incomingRank > currentRank;
    }
}
