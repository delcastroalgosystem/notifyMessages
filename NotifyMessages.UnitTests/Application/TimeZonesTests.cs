using NotifyMessages.Application.Services;

namespace NotifyMessages.UnitTests.Application;

public class TimeZonesTests
{
    [Theory]
    [InlineData("Europe/Lisbon")]
    [InlineData("Atlantic/Azores")]
    [InlineData("Atlantic/Madeira")]
    [InlineData("America/Sao_Paulo")]
    [InlineData("America/Fortaleza")]
    [InlineData("America/Manaus")]
    public void TryResolve_FusosDaInterface(string iana)
        => Assert.True(TimeZones.TryResolve(iana, out _));

    [Fact]
    public void TryResolve_Lisboa_HoraDeVeraoEInverno()
    {
        Assert.True(TimeZones.TryResolve("Europe/Lisbon", out var tz));
        Assert.Equal(TimeSpan.FromHours(1), tz.GetUtcOffset(new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(TimeSpan.Zero, tz.GetUtcOffset(new DateTime(2026, 12, 15, 12, 0, 0, DateTimeKind.Utc)));
    }

    // Os nomes do Windows usados quando o servidor não tem ICU têm de existir e ter as mesmas regras.
    [Theory]
    [InlineData("GMT Standard Time", 1, 0)]              // Lisboa / Madeira
    [InlineData("Azores Standard Time", 0, -1)]
    [InlineData("E. South America Standard Time", -3, -3)]
    public void NomesDoWindows_MesmoDesvio(string windows, int veraoHoras, int invernoHoras)
    {
        if (!OperatingSystem.IsWindows()) return;
        var tz = TimeZoneInfo.FindSystemTimeZoneById(windows);
        Assert.Equal(TimeSpan.FromHours(veraoHoras), tz.GetUtcOffset(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(TimeSpan.FromHours(invernoHoras), tz.GetUtcOffset(new DateTime(2026, 12, 15, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Lua/Crateras")]
    public void TryResolve_Invalido(string? id) => Assert.False(TimeZones.TryResolve(id, out _));
}
