using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Application;

public class DispatchServiceTests
{
    private static DispatchRequestDto BuildRequest(int tenantId = 1, int templateId = 1, string contact = "joao@exemplo.pt", object? businessData = null)
        => new()
        {
            TenantId = tenantId,
            TemplateId = templateId,
            RecipientName = "Joao Teste",
            RecipientContact = contact,
            BusinessData = businessData ?? new { valor = 100 }
        };

    [Fact]
    public async Task EnqueueMessageAsync_NovaRequisicao_CriaMensagemComStatusQueued()
    {
        using var context = TestDbContextFactory.Create();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest());

        var saved = Assert.Single(context.MessageDispatches);
        Assert.Equal(DispatchStatus.Queued, saved.CurrentStatus);
        Assert.False(string.IsNullOrEmpty(saved.IdempotencyKey));
    }

    [Fact]
    public async Task EnqueueMessageAsync_RequisicaoIdentica_LancaExcecaoDeDuplicidade()
    {
        using var context = TestDbContextFactory.Create();
        var service = new DispatchService(context);
        var request = BuildRequest();

        await service.EnqueueMessageAsync(request);

        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.EnqueueMessageAsync(request));
        Assert.StartsWith("DUPLICATE_REQUEST", ex.Message);
        Assert.Single(context.MessageDispatches);
    }

    [Fact]
    public async Task EnqueueMessageAsync_MesmoTenantTemplateContactoComDadosDiferentes_NaoEDuplicado()
    {
        using var context = TestDbContextFactory.Create();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest(businessData: new { valor = 100 }));
        await service.EnqueueMessageAsync(BuildRequest(businessData: new { valor = 200 }));

        Assert.Equal(2, context.MessageDispatches.Count());
    }

    [Fact]
    public async Task EnqueueMessageAsync_ContatoDiferente_GeraChavesDeIdempotenciaDiferentes()
    {
        using var context = TestDbContextFactory.Create();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest(contact: "joao@exemplo.pt"));
        await service.EnqueueMessageAsync(BuildRequest(contact: "maria@exemplo.pt"));

        var keys = context.MessageDispatches.Select(m => m.IdempotencyKey).Distinct();
        Assert.Equal(2, keys.Count());
    }

    [Fact]
    public async Task EnqueueMessageAsync_DevolveIdDoMessageDispatchGravado()
    {
        using var context = TestDbContextFactory.Create();
        var service = new DispatchService(context);

        var trackingId = await service.EnqueueMessageAsync(BuildRequest());

        var saved = Assert.Single(context.MessageDispatches);
        Assert.Equal(saved.Id, trackingId);
    }

    [Fact]
    public async Task EnqueueMessageAsync_RequisicaoIdentica_ExcecaoTrazIdDoEnvioExistente()
    {
        using var context = TestDbContextFactory.Create();
        var service = new DispatchService(context);
        var request = BuildRequest();

        var firstId = await service.EnqueueMessageAsync(request);

        var ex = await Assert.ThrowsAsync<DuplicateDispatchException>(() => service.EnqueueMessageAsync(request));
        Assert.Equal(firstId, ex.ExistingDispatchId);
    }

    [Fact]
    public async Task GetStatusAsync_EnvioDoTenant_DevolveEstadoEEventos()
    {
        using var context = TestDbContextFactory.Create();
        var service = new DispatchService(context);
        var id = await service.EnqueueMessageAsync(BuildRequest(tenantId: 1));
        context.MessageDispatchEvents.Add(new MessageDispatchEvent { DispatchId = id, EventType = "delivered", EventDate = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var status = await service.GetStatusAsync(id, tenantId: 1);

        Assert.NotNull(status);
        Assert.Equal(id, status!.Id);
        Assert.Equal(DispatchStatus.Queued, status.Status);
        Assert.Equal("Queued", status.StatusName);
        Assert.Equal("delivered", Assert.Single(status.Events).EventType);
    }

    [Fact]
    public async Task GetStatusAsync_EnvioDeOutroTenant_DevolveNull()
    {
        using var context = TestDbContextFactory.Create();
        var service = new DispatchService(context);
        var id = await service.EnqueueMessageAsync(BuildRequest(tenantId: 1));

        Assert.Null(await service.GetStatusAsync(id, tenantId: 2));
        Assert.Null(await service.GetStatusAsync(id + 999, tenantId: 1));
    }
}
