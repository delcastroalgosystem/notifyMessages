using NotifyMessages.Application.Services;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.UnitTests.Application;

public class TriggerServiceTests
{
    // 25/09/2026 08:30 UTC = 09:30 em Lisboa (WEST, UTC+1) = 05:30 em São Paulo (UTC-3).
    private static readonly DateTime Agora = new(2026, 9, 25, 8, 30, 0, DateTimeKind.Utc);

    private static MessageTrigger Gatilho(int id, int tenantId = 1, string code = "SMARTARENA.ANIVERSARIO",
        byte? dia = null, string hora = "09:00", DateOnly? inicio = null, bool ativo = true) => new()
    {
        Id = id, TenantId = tenantId, Code = code, Name = $"G{id}", TemplateId = 1,
        ScheduleDay = dia, ScheduleTime = TimeOnly.Parse(hora), StartDate = inicio, IsActive = ativo
    };

    private static NotifyMessages.Infrastructure.Persistence.AppDbContext Contexto(string timeZone = "Europe/Lisbon")
    {
        var context = TestDbContextFactory.Create();
        context.Tenants.Add(new Tenant { Id = 1, Name = "Clube", TimeZone = timeZone });
        context.Tenants.Add(new Tenant { Id = 2, Name = "Outro" });
        return context;
    }

    [Fact]
    public async Task GetDueAsync_Intervalo_DevidoPassadoOIntervaloEIgnoraAAgenda()
    {
        using var context = Contexto();
        // Hora 23:00 e dia 1: num diário não estaria devido; num de intervalo não conta
        var nunca = Gatilho(1, code: "SMARTARENA.REFERENCIA_MB", dia: 1, hora: "23:00", inicio: new DateOnly(2026, 9, 1));
        nunca.IntervalMinutes = 15;
        var ha20 = Gatilho(2, code: "SMARTARENA.REFERENCIA_MB");
        ha20.IntervalMinutes = 15; ha20.LastRunAt = Agora.AddMinutes(-20);
        var ha10 = Gatilho(3, code: "SMARTARENA.REFERENCIA_MB");
        ha10.IntervalMinutes = 15; ha10.LastRunAt = Agora.AddMinutes(-10);
        context.MessageTriggers.AddRange(nunca, ha20, ha10);
        // Um lote de hoje não impede um gatilho de intervalo
        context.DispatchBatches.Add(new DispatchBatch { TenantId = 1, TriggerId = 1, Source = BatchSource.Connector, RunDate = new DateOnly(2026, 9, 25), RunAt = Agora.AddMinutes(-30) });
        context.SaveChanges();

        var due = await new TriggerService(context).GetDueAsync(1, Agora);

        Assert.Equal([1, 2], due.Select(d => d.TriggerId));
        Assert.All(due, d => Assert.Equal(15, d.IntervalMinutes));
        Assert.Equal(new DateOnly(2026, 9, 1), due[0].StartDate);
    }

    [Fact]
    public async Task GetDueAsync_Intervalo_AntesDaDataDeInicio_NaoEDevido()
    {
        using var context = Contexto();
        var g = Gatilho(1, code: "SMARTARENA.REFERENCIA_MB", inicio: new DateOnly(2026, 10, 1));
        g.IntervalMinutes = 15;
        context.MessageTriggers.Add(g);
        context.SaveChanges();

        Assert.Empty(await new TriggerService(context).GetDueAsync(1, Agora));
    }

    [Fact]
    public async Task GetDueAsync_DiarioDepoisDaHoraLocal_EDevidoComRunDateLocal()
    {
        using var context = Contexto();
        context.MessageTriggers.Add(Gatilho(1, hora: "09:00"));
        context.SaveChanges();

        var due = await new TriggerService(context).GetDueAsync(1, Agora);

        var g = Assert.Single(due);
        Assert.Equal(1, g.TriggerId);
        Assert.Equal(new DateOnly(2026, 9, 25), g.RunDate);
    }

    [Fact]
    public async Task GetDueAsync_UsaOFusoDoTenant()
    {
        using var context = Contexto(timeZone: "America/Sao_Paulo");
        context.MessageTriggers.Add(Gatilho(1, hora: "09:00"));
        context.SaveChanges();

        // Em São Paulo ainda são 05:30
        Assert.Empty(await new TriggerService(context).GetDueAsync(1, Agora));
    }

    [Fact]
    public async Task GetDueAsync_AntesDaHora_DiaErrado_AntesDoInicio_Inativo_OuFile_NaoSaoDevidos()
    {
        using var context = Contexto();
        context.MessageTriggers.Add(Gatilho(1, hora: "10:00"));
        context.MessageTriggers.Add(Gatilho(2, dia: 20));
        context.MessageTriggers.Add(Gatilho(3, inicio: new DateOnly(2026, 10, 1)));
        context.MessageTriggers.Add(Gatilho(4, ativo: false));
        context.MessageTriggers.Add(Gatilho(5, code: TriggerService.FileCode));
        context.MessageTriggers.Add(Gatilho(6, dia: 25));
        context.SaveChanges();

        var due = await new TriggerService(context).GetDueAsync(1, Agora);

        Assert.Equal(6, Assert.Single(due).TriggerId);
    }

    [Fact]
    public async Task GetDueAsync_JaTemLoteDeConectorParaODia_NaoEDevido()
    {
        using var context = Contexto();
        context.MessageTriggers.Add(Gatilho(1));
        context.MessageTriggers.Add(Gatilho(2));
        context.DispatchBatches.Add(new DispatchBatch { TenantId = 1, TriggerId = 1, Source = BatchSource.Connector, RunDate = new DateOnly(2026, 9, 25) });
        context.DispatchBatches.Add(new DispatchBatch { TenantId = 1, TriggerId = 2, Source = BatchSource.Connector, RunDate = new DateOnly(2026, 9, 24) });
        context.SaveChanges();

        var due = await new TriggerService(context).GetDueAsync(1, Agora);

        Assert.Equal(2, Assert.Single(due).TriggerId);
    }

    [Fact]
    public async Task GetDueAsync_GatilhosDeOutroTenant_NaoAparecem()
    {
        using var context = Contexto();
        context.MessageTriggers.Add(Gatilho(1, tenantId: 2));
        context.SaveChanges();

        Assert.Empty(await new TriggerService(context).GetDueAsync(1, Agora));
    }

    [Theory]
    [InlineData(null, null, "09:00", 25, "09:00", true)]
    [InlineData(null, null, "09:00", 25, "08:59", false)]
    [InlineData(null, (byte)25, "00:00", 25, "00:00", true)]
    [InlineData(null, (byte)24, "00:00", 25, "12:00", false)]
    [InlineData("2026-09-26", null, "00:00", 25, "12:00", false)]
    [InlineData("2026-09-25", null, "00:00", 25, "12:00", true)]
    public void IsDue(string? inicio, byte? dia, string hora, int hojeDia, string agora, bool esperado)
        => Assert.Equal(esperado, TriggerService.IsDue(
            inicio is null ? null : DateOnly.Parse(inicio), dia, TimeOnly.Parse(hora),
            new DateOnly(2026, 9, hojeDia), TimeOnly.Parse(agora)));
}
