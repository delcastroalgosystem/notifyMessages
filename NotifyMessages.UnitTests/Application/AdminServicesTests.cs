using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Application;

public class AdminServicesTests
{
    private const string Admin = "wendel@smartmove.pt";

    private static NotifyMessages.Infrastructure.Persistence.AppDbContext Contexto()
    {
        var context = TestDbContextFactory.Create();
        context.Tenants.Add(new Tenant { Id = 1, Name = "Clube A", RefName = "CLUBE_A" });
        context.Tenants.Add(new Tenant { Id = 2, Name = "Clube B", RefName = "CLUBE_B" });
        context.Templates.Add(new Template { Id = 1, TenantId = 1, Name = "T1", Channel = ChannelType.Email, ProviderType = ProviderType.Egoi });
        context.Templates.Add(new Template { Id = 2, TenantId = 2, Name = "T2", Channel = ChannelType.Email, ProviderType = ProviderType.Egoi });
        context.Templates.Add(new Template { Id = 3, TenantId = null, Name = "Partilhado", Channel = ChannelType.Email, ProviderType = ProviderType.Egoi });
        context.MessageTriggers.Add(new MessageTrigger { Id = 1, TenantId = 1, Code = "SMARTARENA.AVISO", Name = "Aviso", TemplateId = 1, IsActive = true, RequiresApproval = true });
        context.SaveChanges();
        return context;
    }

    private static BatchItemDto Item(string key, string contact) => new() { ExternalKey = key, RecipientName = "Sócio", RecipientContact = contact };

    private static async Task<BatchDto> LotePendente(NotifyMessages.Infrastructure.Persistence.AppDbContext context, params BatchItemDto[] itens)
        => await new BatchService(context).CreateAsync(1, new BatchCreateDto { TriggerId = 1, RunDate = new DateOnly(2026, 9, 27), Items = itens.ToList() });

    // ---------- Lotes ----------

    [Fact]
    public async Task Approve_LotePendente_HeldPassaAQueuedEGuardaQuemAprovou()
    {
        using var context = Contexto();
        var lote = await LotePendente(context, Item("K1", "a@x.pt"), Item("K2", "b@x.pt"));

        var aprovado = await new BatchService(context).ApproveAsync(lote.Id, Admin);

        Assert.Equal(BatchStatus.Approved, aprovado!.Status);
        Assert.Equal(Admin, aprovado.ApprovedBy);
        Assert.All(context.MessageDispatches, m => Assert.Equal(DispatchStatus.Queued, m.CurrentStatus));
    }

    [Fact]
    public async Task Approve_LoteJaAprovado_LancaRequestValidation()
    {
        using var context = Contexto();
        var lote = await LotePendente(context, Item("K1", "a@x.pt"));
        var service = new BatchService(context);
        await service.ApproveAsync(lote.Id, Admin);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.ApproveAsync(lote.Id, Admin));
    }

    [Fact]
    public async Task Cancel_LotePendente_EnviosCanceladosComMotivo()
    {
        using var context = Contexto();
        var lote = await LotePendente(context, Item("K1", "a@x.pt"));

        var cancelado = await new BatchService(context).CancelAsync(lote.Id, Admin);

        Assert.Equal(BatchStatus.Canceled, cancelado!.Status);
        var m = Assert.Single(context.MessageDispatches);
        Assert.Equal(DispatchStatus.Canceled, m.CurrentStatus);
        Assert.Contains(Admin, m.ErrorLog);
    }

    [Fact]
    public async Task Cancel_LoteAprovado_SoCancelaOQueAindaNaoFoiEnviado()
    {
        using var context = Contexto();
        var lote = await LotePendente(context, Item("K1", "a@x.pt"), Item("K2", "b@x.pt"));
        var service = new BatchService(context);
        await service.ApproveAsync(lote.Id, Admin);
        context.MessageDispatches.Single(m => m.ExternalKey == "K1").CurrentStatus = DispatchStatus.Sent;
        context.SaveChanges();

        await service.CancelAsync(lote.Id, Admin);

        Assert.Equal(DispatchStatus.Sent, context.MessageDispatches.Single(m => m.ExternalKey == "K1").CurrentStatus);
        Assert.Equal(DispatchStatus.Canceled, context.MessageDispatches.Single(m => m.ExternalKey == "K2").CurrentStatus);
    }

    [Fact]
    public async Task Cancel_LoteConcluido_LancaRequestValidation()
    {
        using var context = Contexto();
        var service = new BatchService(context);
        var vazio = await service.CreateAsync(1, new BatchCreateDto { TriggerId = 1, RunDate = new DateOnly(2026, 9, 27) });

        await Assert.ThrowsAsync<RequestValidationException>(() => service.CancelAsync(vazio.Id, Admin));
    }

    [Fact]
    public async Task ListEItems_FiltramPorTenantEEstado()
    {
        using var context = Contexto();
        var lote = await LotePendente(context, Item("K1", "a@x.pt"), Item("K2", "b@x.pt"));
        var service = new BatchService(context);

        var doTenant1 = await service.ListAsync(new BatchQueryDto { TenantId = 1 });
        var doTenant2 = await service.ListAsync(new BatchQueryDto { TenantId = 2 });
        var pendentes = await service.ListAsync(new BatchQueryDto { Status = BatchStatus.PendingApproval });
        var itens = await service.ItemsAsync(lote.Id, DispatchStatus.Held, 1, 1);

        Assert.Equal(1, doTenant1.Total);
        Assert.Equal(2, doTenant1.Items.Single().Progress["Held"]);
        Assert.Equal(0, doTenant2.Total);
        Assert.Equal(1, pendentes.Total);
        Assert.Equal((2, 1), (itens!.Total, itens.Items.Count));
    }

    [Fact]
    public async Task CreateFromFile_FicaPendenteComFonteFileEChavesGeradas()
    {
        using var context = Contexto();
        var parsed = new ParsedBatchFile { Items = [Item("", "a@x.pt"), Item("MINHA:1", "b@x.pt")], Errors = ["Linha 9 ignorada"] };

        var lote = await new BatchService(context).CreateFromFileAsync(2, null, 3, "lista.csv", parsed, Admin);

        Assert.Equal((BatchSource.File, BatchStatus.PendingApproval, "lista.csv", Admin), (lote.Source, lote.Status, lote.FileName, lote.CreatedBy));
        Assert.Equal(2, lote.Accepted);
        Assert.Contains(context.MessageDispatches, m => m.ExternalKey!.StartsWith("FILE:T3:") && m.ExternalKey.EndsWith(":a@x.pt"));
        Assert.Contains(context.MessageDispatches, m => m.ExternalKey == "MINHA:1");
        Assert.Contains("Linha 9 ignorada", lote.Details);
    }

    [Fact]
    public async Task CreateFromFile_MesmoFicheiroNoMesmoDia_NaoDuplica()
    {
        using var context = Contexto();
        var service = new BatchService(context);
        ParsedBatchFile Ficheiro() => new() { Items = [Item("", "a@x.pt")] };

        await service.CreateFromFileAsync(2, null, 3, "lista.csv", Ficheiro(), Admin);
        var segundo = await service.CreateFromFileAsync(2, null, 3, "lista.csv", Ficheiro(), Admin);

        Assert.Equal((0, 1), (segundo.Accepted, segundo.Duplicates));
    }

    // ---------- Gatilhos ----------

    private static TriggerUpsertDto Gatilho(int tenantId = 1, int templateId = 1, string name = "Aniversário", byte? dia = null) => new()
    {
        TenantId = tenantId, Code = "SMARTARENA.ANIVERSARIO", Name = name, TemplateId = templateId,
        ScheduleDay = dia, ScheduleTime = new TimeOnly(8, 0), IsActive = true, Parameters = """{"minimoQuotas":2}"""
    };

    [Fact]
    public async Task TriggerCreate_GuardaQuemCriou()
    {
        using var context = Contexto();

        var t = await new TriggerService(context).CreateAsync(Gatilho(templateId: 3), Admin);

        Assert.Equal((Admin, 1, 3), (t.CreatedBy, t.TenantId, t.TemplateId));
    }

    [Fact]
    public async Task TriggerCreate_Validacoes()
    {
        using var context = Contexto();
        var service = new TriggerService(context);

        await Assert.ThrowsAsync<TemplateNotAvailableException>(() => service.CreateAsync(Gatilho(templateId: 2), Admin));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(Gatilho(tenantId: 99), Admin));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(Gatilho(name: "Aviso"), Admin));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(Gatilho(dia: 30), Admin));
        var jsonInvalido = Gatilho(name: "Outro");
        jsonInvalido.Parameters = "{nao json";
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CreateAsync(jsonInvalido, Admin));
    }

    [Fact]
    public async Task TriggerUpdate_AlteraEGuardaQuemAtualizou()
    {
        using var context = Contexto();
        var service = new TriggerService(context);
        var pedido = Gatilho(name: "Aviso");
        pedido.IsActive = false;

        var t = await service.UpdateAsync(1, pedido, Admin);

        Assert.False(t!.IsActive);
        Assert.Equal(Admin, t.UpdatedBy);
        Assert.Null(await service.UpdateAsync(999, pedido, Admin));
    }

    // ---------- Supressões ----------

    [Fact]
    public async Task SuppressionAdd_NormalizaESuprimeOsEnviosPendentesDoContacto()
    {
        using var context = Contexto();
        await LotePendente(context, Item("K1", "Socio@X.pt"), Item("K2", "outro@x.pt"));

        var r = await new SuppressionService(context).AddAsync(new SuppressionCreateDto { TenantId = 1, Contact = "  SOCIO@x.PT ", Reason = "Pediu" }, Admin);

        Assert.Equal(("socio@x.pt", false, 1), (r.Suppression.Contact, r.AlreadyExisted, r.PendingDispatchesSuppressed));
        Assert.Equal(DispatchStatus.Suppressed, context.MessageDispatches.Single(m => m.ExternalKey == "K1").CurrentStatus);
        Assert.Equal(DispatchStatus.Held, context.MessageDispatches.Single(m => m.ExternalKey == "K2").CurrentStatus);
    }

    [Fact]
    public async Task SuppressionAdd_Repetido_EIdempotente()
    {
        using var context = Contexto();
        var service = new SuppressionService(context);
        await service.AddAsync(new SuppressionCreateDto { TenantId = 1, Contact = "a@x.pt" }, Admin);

        var r = await service.AddAsync(new SuppressionCreateDto { TenantId = 1, Contact = "A@x.pt" }, Admin);

        Assert.True(r.AlreadyExisted);
        Assert.Single(context.Suppressions);
    }

    [Fact]
    public async Task SuppressionAdd_ContactoInvalido_Lanca()
    {
        using var context = Contexto();
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            new SuppressionService(context).AddAsync(new SuppressionCreateDto { TenantId = 1, Contact = "nao-e-contacto" }, Admin));
    }

    [Fact]
    public async Task SuppressionListERemove()
    {
        using var context = Contexto();
        var service = new SuppressionService(context);
        var r = await service.AddAsync(new SuppressionCreateDto { TenantId = 1, Contact = "a@x.pt" }, Admin);
        await service.AddAsync(new SuppressionCreateDto { TenantId = 2, Contact = "b@x.pt" }, Admin);

        Assert.Equal(1, (await service.ListAsync(1, null, 1, 50)).Total);
        Assert.Equal(1, (await service.ListAsync(null, "B@X", 1, 50)).Total);
        Assert.True(await service.RemoveAsync(r.Suppression.Id));
        Assert.False(await service.RemoveAsync(r.Suppression.Id));
    }

    // ---------- Tenants ----------

    [Fact]
    public async Task TenantSettings_AtualizaEValida()
    {
        using var context = Contexto();
        var service = new TenantAdminService(context);

        var t = await service.UpdateSettingsAsync(1, new TenantSettingsDto { SendingEnabled = false, SandboxContact = " teste@x.pt ", TimeZone = "America/Sao_Paulo" });

        Assert.Equal((false, "teste@x.pt", "America/Sao_Paulo"), (t!.SendingEnabled, t.SandboxContact, t.TimeZone));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.UpdateSettingsAsync(1, new TenantSettingsDto { TimeZone = "Marte/Olympus" }));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.UpdateSettingsAsync(1, new TenantSettingsDto { SandboxContact = "invalido" }));
        Assert.Null(await service.UpdateSettingsAsync(99, new TenantSettingsDto()));
    }

    // ---------- Histórico ----------

    [Fact]
    public async Task DispatchSearch_FiltraPaginaEContaPorEstado()
    {
        using var context = Contexto();
        await LotePendente(context, Item("SOCIO:1:2026", "a@x.pt"), Item("SOCIO:2:2026", "b@x.pt"), Item("SOCIO:3:2026", "c@x.pt"));
        context.MessageDispatches.Single(m => m.ExternalKey == "SOCIO:3:2026").CurrentStatus = DispatchStatus.Sent;
        context.SaveChanges();
        var service = new DispatchQueryService(context);

        var held = await service.SearchAsync(new DispatchQueryDto { TenantId = 1, Status = DispatchStatus.Held, PageSize = 1 });
        var procura = await service.SearchAsync(new DispatchQueryDto { Search = "SOCIO:2" });
        var outroTenant = await service.SearchAsync(new DispatchQueryDto { TenantId = 2 });

        Assert.Equal((2, 1), (held.Total, held.Items.Count));
        Assert.Equal(2, held.CountsByStatus["Held"]);
        Assert.Equal(1, held.CountsByStatus["Sent"]);
        Assert.Equal("SOCIO:2:2026", Assert.Single(procura.Items).ExternalKey);
        Assert.Equal(0, outroTenant.Total);
    }
}
