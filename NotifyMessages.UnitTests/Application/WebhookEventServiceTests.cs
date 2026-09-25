using Microsoft.Extensions.Logging.Abstractions;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Application;

public class WebhookEventServiceTests
{
    private static MessageDispatch BuildDispatch(DispatchStatus status, string? externalId = "EXT-1")
        => new()
        {
            TenantId = 1,
            TemplateId = 1,
            IdempotencyKey = Guid.NewGuid().ToString(),
            RecipientName = "Cliente",
            RecipientContact = "cliente@x.pt",
            CurrentStatus = status,
            ExternalId = externalId
        };

    [Fact]
    public async Task ProcessEventAsync_EventoAvancaStatus_AtualizaDispatchEGravaEvento()
    {
        using var context = TestDbContextFactory.Create();
        var dispatch = BuildDispatch(DispatchStatus.Sent);
        context.MessageDispatches.Add(dispatch);
        context.SaveChanges();

        var service = new WebhookEventService(context, NullLogger<WebhookEventService>.Instance);

        await service.ProcessEventAsync(new WebhookEventDto
        {
            DispatchId = dispatch.Id,
            EventType = "delivered",
            MappedStatus = DispatchStatus.Delivered,
            EventDate = DateTime.UtcNow,
            RawPayload = "{}"
        });

        Assert.Equal(DispatchStatus.Delivered, dispatch.CurrentStatus);
        Assert.NotNull(dispatch.ProcessedAt);
        var evt = Assert.Single(context.MessageDispatchEvents);
        Assert.Equal(dispatch.Id, evt.DispatchId);
        Assert.Equal("delivered", evt.EventType);
    }

    [Fact]
    public async Task ProcessEventAsync_EventoRegrediriaStatus_NaoAtualizaStatusMasGravaEvento()
    {
        using var context = TestDbContextFactory.Create();
        var dispatch = BuildDispatch(DispatchStatus.Read);
        context.MessageDispatches.Add(dispatch);
        context.SaveChanges();

        var service = new WebhookEventService(context, NullLogger<WebhookEventService>.Instance);

        await service.ProcessEventAsync(new WebhookEventDto
        {
            DispatchId = dispatch.Id,
            EventType = "delivered",
            MappedStatus = DispatchStatus.Delivered,
            EventDate = DateTime.UtcNow,
            RawPayload = "{}"
        });

        Assert.Equal(DispatchStatus.Read, dispatch.CurrentStatus);
        Assert.Single(context.MessageDispatchEvents);
    }

    [Fact]
    public async Task ProcessEventAsync_EventoTerminalBounced_SobrepoeStatusMesmoAvancado()
    {
        using var context = TestDbContextFactory.Create();
        var dispatch = BuildDispatch(DispatchStatus.Read);
        context.MessageDispatches.Add(dispatch);
        context.SaveChanges();

        var service = new WebhookEventService(context, NullLogger<WebhookEventService>.Instance);

        await service.ProcessEventAsync(new WebhookEventDto
        {
            DispatchId = dispatch.Id,
            EventType = "spam_complaint",
            MappedStatus = DispatchStatus.Bounced,
            EventDate = DateTime.UtcNow,
            RawPayload = "{}"
        });

        Assert.Equal(DispatchStatus.Bounced, dispatch.CurrentStatus);
    }

    [Fact]
    public async Task ProcessEventAsync_DispatchJaTerminal_IgnoraNovosEventosDeStatus()
    {
        using var context = TestDbContextFactory.Create();
        var dispatch = BuildDispatch(DispatchStatus.Failed);
        context.MessageDispatches.Add(dispatch);
        context.SaveChanges();

        var service = new WebhookEventService(context, NullLogger<WebhookEventService>.Instance);

        await service.ProcessEventAsync(new WebhookEventDto
        {
            DispatchId = dispatch.Id,
            EventType = "delivered",
            MappedStatus = DispatchStatus.Delivered,
            EventDate = DateTime.UtcNow,
            RawPayload = "{}"
        });

        Assert.Equal(DispatchStatus.Failed, dispatch.CurrentStatus);
        Assert.Single(context.MessageDispatchEvents);
    }

    [Fact]
    public async Task ProcessEventAsync_SemDispatchIdMasComExternalIdCorrespondente_EncontraPorFallback()
    {
        using var context = TestDbContextFactory.Create();
        var dispatch = BuildDispatch(DispatchStatus.Sent, externalId: "SG-EXTERNAL-123");
        context.MessageDispatches.Add(dispatch);
        context.SaveChanges();

        var service = new WebhookEventService(context, NullLogger<WebhookEventService>.Instance);

        await service.ProcessEventAsync(new WebhookEventDto
        {
            DispatchId = null,
            ExternalId = "SG-EXTERNAL-123",
            EventType = "delivered",
            MappedStatus = DispatchStatus.Delivered,
            EventDate = DateTime.UtcNow,
            RawPayload = "{}"
        });

        Assert.Equal(DispatchStatus.Delivered, dispatch.CurrentStatus);
    }

    [Fact]
    public async Task ProcessEventAsync_SemCorrespondencia_NaoLancaExcecaoNemGravaEvento()
    {
        using var context = TestDbContextFactory.Create();
        var service = new WebhookEventService(context, NullLogger<WebhookEventService>.Instance);

        await service.ProcessEventAsync(new WebhookEventDto
        {
            DispatchId = 999,
            ExternalId = "nao-existe",
            EventType = "delivered",
            MappedStatus = DispatchStatus.Delivered,
            EventDate = DateTime.UtcNow,
            RawPayload = "{}"
        });

        Assert.Empty(context.MessageDispatchEvents);
    }
}
