using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Application;

public class DispatchServiceTests
{
    // Template 1 partilhado (TENANT_ID nulo), template 2 do tenant 2.
    private static NotifyMessages.Infrastructure.Persistence.AppDbContext CriarContexto()
    {
        var context = TestDbContextFactory.Create();
        context.Templates.Add(new Template { Id = 1, Name = "Partilhado", Channel = ChannelType.Email, ProviderType = ProviderType.SendGrid, IsActive = true });
        context.Templates.Add(new Template { Id = 2, TenantId = 2, Name = "Do tenant 2", Channel = ChannelType.Email, ProviderType = ProviderType.SendGrid, IsActive = true });
        context.SaveChanges();
        return context;
    }

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
        using var context = CriarContexto();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest());

        var saved = Assert.Single(context.MessageDispatches);
        Assert.Equal(DispatchStatus.Queued, saved.CurrentStatus);
        Assert.False(string.IsNullOrEmpty(saved.IdempotencyKey));
    }

    [Fact]
    public async Task EnqueueMessageAsync_RequisicaoIdentica_LancaExcecaoDeDuplicidade()
    {
        using var context = CriarContexto();
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
        using var context = CriarContexto();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest(businessData: new { valor = 100 }));
        await service.EnqueueMessageAsync(BuildRequest(businessData: new { valor = 200 }));

        Assert.Equal(2, context.MessageDispatches.Count());
    }

    [Fact]
    public async Task EnqueueMessageAsync_ContatoDiferente_GeraChavesDeIdempotenciaDiferentes()
    {
        using var context = CriarContexto();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest(contact: "joao@exemplo.pt"));
        await service.EnqueueMessageAsync(BuildRequest(contact: "maria@exemplo.pt"));

        var keys = context.MessageDispatches.Select(m => m.IdempotencyKey).Distinct();
        Assert.Equal(2, keys.Count());
    }

    [Fact]
    public async Task EnqueueMessageAsync_DevolveIdDoMessageDispatchGravado()
    {
        using var context = CriarContexto();
        var service = new DispatchService(context);

        var trackingId = await service.EnqueueMessageAsync(BuildRequest());

        var saved = Assert.Single(context.MessageDispatches);
        Assert.Equal(saved.Id, trackingId);
    }

    [Fact]
    public async Task EnqueueMessageAsync_RequisicaoIdentica_ExcecaoTrazIdDoEnvioExistente()
    {
        using var context = CriarContexto();
        var service = new DispatchService(context);
        var request = BuildRequest();

        var firstId = await service.EnqueueMessageAsync(request);

        var ex = await Assert.ThrowsAsync<DuplicateDispatchException>(() => service.EnqueueMessageAsync(request));
        Assert.Equal(firstId, ex.ExistingDispatchId);
    }

    [Fact]
    public async Task GetStatusAsync_EnvioDoTenant_DevolveEstadoEEventos()
    {
        using var context = CriarContexto();
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
        using var context = CriarContexto();
        var service = new DispatchService(context);
        var id = await service.EnqueueMessageAsync(BuildRequest(tenantId: 1));

        Assert.Null(await service.GetStatusAsync(id, tenantId: 2));
        Assert.Null(await service.GetStatusAsync(id + 999, tenantId: 1));
    }

    [Fact]
    public async Task EnqueueMessageAsync_TemplateDeOutroTenant_LancaTemplateNotAvailable()
    {
        using var context = CriarContexto();
        var service = new DispatchService(context);

        await Assert.ThrowsAsync<TemplateNotAvailableException>(() => service.EnqueueMessageAsync(BuildRequest(tenantId: 1, templateId: 2)));
        Assert.Empty(context.MessageDispatches);
    }

    [Fact]
    public async Task EnqueueMessageAsync_TemplateDoProprioTenant_Aceita()
    {
        using var context = CriarContexto();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest(tenantId: 2, templateId: 2));

        Assert.Single(context.MessageDispatches);
    }

    [Fact]
    public async Task EnqueueMessageAsync_MesmaExternalKeyComDadosDiferentes_EDuplicado()
    {
        using var context = CriarContexto();
        var service = new DispatchService(context);
        var primeiro = BuildRequest(businessData: new { valor = 100 });
        primeiro.ExternalKey = "SOCIO:1234:2026";
        var segundo = BuildRequest(businessData: new { valor = 200 });
        segundo.ExternalKey = "SOCIO:1234:2026";

        var firstId = await service.EnqueueMessageAsync(primeiro);

        var ex = await Assert.ThrowsAsync<DuplicateDispatchException>(() => service.EnqueueMessageAsync(segundo));
        Assert.Equal(firstId, ex.ExistingDispatchId);
        Assert.Equal("SOCIO:1234:2026", Assert.Single(context.MessageDispatches).ExternalKey);
    }

    [Fact]
    public async Task EnqueueMessageAsync_MesmaExternalKeyEmTenantsDiferentes_NaoEDuplicado()
    {
        using var context = CriarContexto();
        var service = new DispatchService(context);
        var doTenant1 = BuildRequest(tenantId: 1, templateId: 1);
        doTenant1.ExternalKey = "SOCIO:1:2026";
        var doTenant2 = BuildRequest(tenantId: 2, templateId: 2);
        doTenant2.ExternalKey = "SOCIO:1:2026";

        await service.EnqueueMessageAsync(doTenant1);
        await service.EnqueueMessageAsync(doTenant2);

        Assert.Equal(2, context.MessageDispatches.Count());
    }

    [Fact]
    public async Task EnqueueMessageAsync_ContactoSuprimido_FicaSuppressedENaoEntraNaFila()
    {
        using var context = CriarContexto();
        context.Suppressions.Add(new Suppression { TenantId = 1, Contact = "joao@exemplo.pt" });
        context.SaveChanges();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest(contact: "  Joao@Exemplo.PT "));

        Assert.Equal(DispatchStatus.Suppressed, Assert.Single(context.MessageDispatches).CurrentStatus);
    }

    [Fact]
    public async Task EnqueueMessageAsync_SupressaoDeOutroTenant_NaoAfeta()
    {
        using var context = CriarContexto();
        context.Suppressions.Add(new Suppression { TenantId = 2, Contact = "joao@exemplo.pt" });
        context.SaveChanges();
        var service = new DispatchService(context);

        await service.EnqueueMessageAsync(BuildRequest(tenantId: 1));

        Assert.Equal(DispatchStatus.Queued, Assert.Single(context.MessageDispatches).CurrentStatus);
    }
}
