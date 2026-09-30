namespace NotifyMessages.Application.Services;

// Hora local de um tenant (TENANT.TIME_ZONE, IANA). As agendas dos gatilhos são sempre em hora local.
public static class TenantClock
{
    public static DateTime ToLocal(DateTime utcNow, string? timeZoneId)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Resolve(timeZoneId));
    }

    // Fuso inválido ou desconhecido: Europe/Lisbon (o valor por omissão do TENANT).
    private static TimeZoneInfo Resolve(string? timeZoneId)
    {
        foreach (var id in new[] { timeZoneId, "Europe/Lisbon" })
        {
            if (TimeZones.TryResolve(id, out var tz))
            {
                return tz;
            }
        }
        return TimeZoneInfo.Utc;
    }
}
