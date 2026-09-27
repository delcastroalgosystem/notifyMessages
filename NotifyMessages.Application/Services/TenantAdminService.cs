using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Application.Validators;
using NotifyMessages.Domain.Entities;

namespace NotifyMessages.Application.Services;

// Tenants para a administração: consulta e definições de envio (o registo continua no Register-Tenant.ps1,
// porque gera a X-Api-Key e trata dos segredos do provedor).
public class TenantAdminService : ITenantAdminService
{
    private readonly IAppDbContext _context;

    public TenantAdminService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TenantAdminDto>> ListAsync(CancellationToken ct = default)
        => (await _context.Tenants.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<TenantAdminDto?> UpdateSettingsAsync(int tenantId, TenantSettingsDto settings, CancellationToken ct = default)
    {
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null)
        {
            return null;
        }

        string? sandbox = string.IsNullOrWhiteSpace(settings.SandboxContact) ? null : settings.SandboxContact.Trim();
        if (sandbox != null && !ContactRules.IsValid(sandbox))
        {
            throw new RequestValidationException("Endereço de Sandbox inválido", "Indique um e-mail válido (ou vazio para desligar o Sandbox do tenant).");
        }
        string timeZone = string.IsNullOrWhiteSpace(settings.TimeZone) ? "Europe/Lisbon" : settings.TimeZone.Trim();
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out _))
        {
            throw new RequestValidationException("Fuso horário inválido", $"'{timeZone}' não é um fuso horário IANA conhecido (ex. Europe/Lisbon, America/Sao_Paulo).");
        }

        tenant.SendingEnabled = settings.SendingEnabled;
        tenant.SandboxContact = sandbox;
        tenant.TimeZone = timeZone;
        await _context.SaveChangesAsync(ct);
        return ToDto(tenant);
    }

    private static TenantAdminDto ToDto(Tenant t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        RefName = t.RefName,
        Active = t.Active,
        SendingEnabled = t.SendingEnabled,
        SandboxContact = t.SandboxContact,
        TimeZone = t.TimeZone
    };
}
