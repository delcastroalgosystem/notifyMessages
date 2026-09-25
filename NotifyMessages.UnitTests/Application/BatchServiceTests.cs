using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Application;

public class BatchServiceTests
{
    private static readonly DateOnly Dia = new(2026, 9, 25);

    // Tenant 1 com template 1 e gatilhos 1 (sem aprovação) e 2 (com aprovação); tenant 2 com template 2 e gatilho 3.
    private static NotifyMessages.Infrastructure.Persistence.AppDbContext Contexto()
    {
        var context = TestDbContextFactory.Create();
        context.Templates.Add(new Template { Id = 1, TenantId = 1, Name = "T1", Channel = ChannelType.Email, ProviderType = ProviderType.Egoi });
        context.Templates.Add(new Template { Id = 2, TenantId = 2, Name = "T2", Channel = ChannelType.Email, ProviderType = ProviderType.Egoi });
        context.MessageTriggers.Add(new MessageTrigger { Id = 1, TenantId = 1, Code = "SMARTARENA.ANIVERSARIO", Name = "Aniversário", TemplateId = 1, IsActive = true });
        context.MessageTriggers.Add(new MessageTrigger { Id = 2, TenantId = 1, Code = "SMARTARENA.AVISO", Name = "Aviso", TemplateId = 1, IsActive = true, RequiresApproval = true });
        context.MessageTriggers.Add(new MessageTrigger { Id = 3, TenantId = 2, Code = "SMARTARENA.ANIVERSARIO", Name = "Aniversário", TemplateId = 2, IsActive = true });
        context.SaveChanges();
        return context;
    }

    private static BatchItemDto Item(string key, string contact = "socio@clube.pt", string name = "Sócio") => new()
    {
        ExternalKey = key, RecipientName = name, RecipientContact = contact,
        BusinessData = new Dictionary<string, string> { ["Nome"] = name }
    };

    private static BatchCreateDto LoteDoGatilho(int triggerId, params BatchItemDto[] itens) => new()
    {
        TriggerId = triggerId, RunDate = Dia, Items = itens.ToList()
    };

    [Fact]
    public async Task CreateAsync_GatilhoSemAprovacao_ItensQueuedELoteAprovado()
    {
        using var context = Contexto();

        var batch = await new BatchService(context).CreateAsync(1, LoteDoGatilho(1, Item("SOCIO:1:2026", "a@x.pt"), Item("SOCIO:2:2026", "b@x.pt")));

        Assert.Equal(BatchStatus.Approved, batch.Status);
        Assert.Equal(BatchSource.Connector, batch.Source);
        Assert.Equal((2, 2), (batch.Received, batch.Accepted));
        Assert.Equal(2, batch.Progress["Queued"]);
        Assert.All(context.MessageDispatches, m =>
        {
            Assert.Equal(batch.Id, m.BatchId);
            Assert.Equal(1, m.TriggerId);
            Assert.Equal(1, m.TemplateId);
            Assert.Equal(DispatchStatus.Queued, m.CurrentStatus);
        });
    }

    [Fact]
    public async Task CreateAsync_GatilhoComAprovacao_ItensHeldELotePendente()
    {
        using var context = Contexto();

        var batch = await new BatchService(context).CreateAsync(1, LoteDoGatilho(2, Item("AVISO:1:2026-10")));

        Assert.Equal(BatchStatus.PendingApproval, batch.Status);
        Assert.Equal(DispatchStatus.Held, Assert.Single(context.MessageDispatches).CurrentStatus);
    }

    [Fact]
    public async Task CreateAsync_MesmoGatilhoEDia_LancaDuplicateBatchComOIdExistente()
    {
        using var context = Contexto();
        var service = new BatchService(context);
        var primeiro = await service.CreateAsync(1, LoteDoGatilho(1, Item("SOCIO:1:2026")));

        var ex = await Assert.ThrowsAsync<DuplicateBatchException>(() => service.CreateAsync(1, LoteDoGatilho(1, Item("SOCIO:9:2026"))));

        Assert.Equal(primeiro.Id, ex.ExistingBatchId);
        Assert.Single(context.MessageDispatches);
    }

    [Fact]
    public async Task CreateAsync_DuplicadosNoLoteEJaEnviadosAntes_NaoCriamEnvios()
    {
        using var context = Contexto();
        context.MessageDispatches.Add(new MessageDispatch
        {
            TenantId = 1, TemplateId = 1, ExternalKey = "SOCIO:1:2026", IdempotencyKey = "x",
            RecipientName = "Antigo", RecipientContact = "a@x.pt", CurrentStatus = DispatchStatus.Sent
        });
        context.SaveChanges();

        var batch = await new BatchService(context).CreateAsync(1, LoteDoGatilho(1,
            Item("SOCIO:1:2026", "a@x.pt"), Item("SOCIO:2:2026", "b@x.pt"), Item("SOCIO:2:2026", "b@x.pt")));

        Assert.Equal((3, 1, 2), (batch.Received, batch.Accepted, batch.Duplicates));
        Assert.Equal(2, context.MessageDispatches.Count());
    }

    [Fact]
    public async Task CreateAsync_ContactoSuprimido_FicaSuppressedEContaAParte()
    {
        using var context = Contexto();
        context.Suppressions.Add(new Suppression { TenantId = 1, Contact = "b@x.pt" });
        context.SaveChanges();

        var batch = await new BatchService(context).CreateAsync(1, LoteDoGatilho(1, Item("SOCIO:1:2026", "a@x.pt"), Item("SOCIO:2:2026", " B@X.pt ")));

        Assert.Equal((1, 1), (batch.Accepted, batch.Suppressed));
        Assert.Equal(DispatchStatus.Suppressed, context.MessageDispatches.Single(m => m.ExternalKey == "SOCIO:2:2026").CurrentStatus);
    }

    [Fact]
    public async Task CreateAsync_ItensInvalidos_SaoRejeitadosComMotivoNosDetalhes()
    {
        using var context = Contexto();

        var batch = await new BatchService(context).CreateAsync(1, LoteDoGatilho(1,
            Item("", "a@x.pt"), Item("SOCIO:2:2026", "nao-e-contacto"), Item("SOCIO:3:2026", "c@x.pt", name: " "), Item("SOCIO:4:2026", "d@x.pt")));

        Assert.Equal((4, 1, 3), (batch.Received, batch.Accepted, batch.Rejected));
        Assert.Contains("RecipientContact inválido", batch.Details);
        Assert.Contains("ExternalKey em falta", batch.Details);
    }

    [Fact]
    public async Task CreateAsync_GuardaExcluidosEAvisosDaOrigemNosDetalhes()
    {
        using var context = Contexto();
        var pedido = LoteDoGatilho(1, Item("SOCIO:1:2026"));
        pedido.Excluded.Add(new BatchExcludedDto { Reference = "SOCIO:7", Reason = "Sem quota para o mês de referência" });
        pedido.Warnings.Add("Quotas da época 2026/2027 ainda não geradas");

        var batch = await new BatchService(context).CreateAsync(1, pedido);

        Assert.Contains("Sem quota para o mês de referência", batch.Details);
        Assert.Contains("ainda não geradas", batch.Details);
    }

    [Fact]
    public async Task CreateAsync_SemNadaParaEnviar_LoteFicaConcluido()
    {
        using var context = Contexto();
        var pedido = LoteDoGatilho(1);
        pedido.Warnings.Add("Nenhum aniversariante hoje");

        var batch = await new BatchService(context).CreateAsync(1, pedido);

        Assert.Equal(BatchStatus.Completed, batch.Status);
        Assert.Empty(context.MessageDispatches);
    }

    [Fact]
    public async Task CreateAsync_GatilhoDeOutroTenant_LancaRequestValidation()
    {
        using var context = Contexto();

        await Assert.ThrowsAsync<RequestValidationException>(() => new BatchService(context).CreateAsync(1, LoteDoGatilho(3, Item("SOCIO:1:2026"))));
    }

    [Fact]
    public async Task CreateAsync_LoteDeApiComTemplateDeOutroTenant_LancaTemplateNotAvailable()
    {
        using var context = Contexto();
        var pedido = new BatchCreateDto { TemplateId = 2, Items = [Item("K1")] };

        await Assert.ThrowsAsync<TemplateNotAvailableException>(() => new BatchService(context).CreateAsync(1, pedido));
    }

    [Fact]
    public async Task CreateAsync_LoteDeGatilhoSemRunDate_LancaRequestValidation()
    {
        using var context = Contexto();
        var pedido = new BatchCreateDto { TriggerId = 1, Items = [Item("K1")] };

        await Assert.ThrowsAsync<RequestValidationException>(() => new BatchService(context).CreateAsync(1, pedido));
    }

    [Fact]
    public async Task GetAsync_TodosOsEnviosTerminados_MarcaLoteComoConcluido()
    {
        using var context = Contexto();
        var service = new BatchService(context);
        var batch = await service.CreateAsync(1, LoteDoGatilho(1, Item("SOCIO:1:2026")));
        context.MessageDispatches.Single().CurrentStatus = DispatchStatus.Sent;
        context.SaveChanges();

        var atual = await service.GetAsync(1, batch.Id);

        Assert.Equal(BatchStatus.Completed, atual!.Status);
        Assert.Equal(1, atual.Progress["Sent"]);
        Assert.NotNull(atual.CompletedAt);
    }

    [Fact]
    public async Task GetAsync_LoteDeOutroTenant_DevolveNull()
    {
        using var context = Contexto();
        var service = new BatchService(context);
        var batch = await service.CreateAsync(1, LoteDoGatilho(1, Item("SOCIO:1:2026")));

        Assert.Null(await service.GetAsync(2, batch.Id));
    }

    [Fact]
    public async Task CreateAsync_MesmaChaveQueOEnvioIndividual_EDuplicada()
    {
        using var context = Contexto();
        await new DispatchService(context).EnqueueMessageAsync(new DispatchRequestDto
        {
            TenantId = 1, TemplateId = 1, RecipientName = "S", RecipientContact = "a@x.pt", ExternalKey = "SOCIO:1:2026"
        });

        var batch = await new BatchService(context).CreateAsync(1, LoteDoGatilho(1, Item("SOCIO:1:2026", "a@x.pt")));

        Assert.Equal((0, 1), (batch.Accepted, batch.Duplicates));
    }
}
