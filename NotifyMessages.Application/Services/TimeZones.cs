namespace NotifyMessages.Application.Services;

// Fusos IANA (TENANT.TIME_ZONE) também em servidores Windows sem ICU (ex. Windows Server 2016): aí o .NET só
// conhece os nomes do Windows e "Europe/Lisbon" não é encontrado. Tenta o nome tal como está e, se falhar,
// o equivalente do Windows (mesmas regras de hora de verão).
public static class TimeZones
{
    private static readonly Dictionary<string, string> IanaParaWindows = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Europe/Lisbon"] = "GMT Standard Time",
        ["Atlantic/Madeira"] = "GMT Standard Time",
        ["Europe/London"] = "GMT Standard Time",
        ["Atlantic/Azores"] = "Azores Standard Time",
        ["Europe/Madrid"] = "Romance Standard Time",
        ["Europe/Paris"] = "Romance Standard Time",
        ["America/Sao_Paulo"] = "E. South America Standard Time",
        ["America/Fortaleza"] = "SA Eastern Standard Time",
        ["America/Manaus"] = "SA Western Standard Time",
        ["Africa/Luanda"] = "W. Central Africa Standard Time",
        ["Africa/Maputo"] = "South Africa Standard Time",
        ["Etc/UTC"] = "UTC",
        ["UTC"] = "UTC"
    };

    public static bool TryResolve(string? id, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        string nome = id.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(nome, out var encontrado))
        {
            timeZone = encontrado;
            return true;
        }
        if (IanaParaWindows.TryGetValue(nome, out var windows) && TimeZoneInfo.TryFindSystemTimeZoneById(windows, out encontrado))
        {
            timeZone = encontrado;
            return true;
        }
        return false;
    }
}
