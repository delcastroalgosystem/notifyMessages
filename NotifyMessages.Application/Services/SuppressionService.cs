using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Application.Validators;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Services;

// Lista de supressão por tenant (opt-out). Os contactos ficam normalizados (ContactNormalizer).
public class SuppressionService : ISuppressionService
{
    private readonly IAppDbContext _context;

    public SuppressionService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<SuppressionDto>> ListAsync(int? tenantId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = BatchService.Paging(page, pageSize);
        string? termo = string.IsNullOrWhiteSpace(search) ? null : ContactNormalizer.Normalize(search);
        var q = _context.Suppressions.AsNoTracking()
            .Where(s => tenantId == null || s.TenantId == tenantId)
            .Where(s => termo == null || s.Contact.Contains(termo));
        int total = await q.CountAsync(ct);
        var itens = await q.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResultDto<SuppressionDto> { Items = itens.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    // Idempotente: um contacto já suprimido devolve o registo existente. Os envios desse contacto ainda
    // por enviar (Held/Queued) passam logo a Suppressed.
    public async Task<SuppressionResultDto> AddAsync(SuppressionCreateDto request, string actingUser, CancellationToken ct = default)
    {
        if (!await _context.Tenants.AnyAsync(t => t.Id == request.TenantId, ct))
        {
            throw new RequestValidationException("Tenant inválido", $"Tenant {request.TenantId} não existe.");
        }
        if (!ContactRules.IsValid(request.Contact?.Trim()))
        {
            throw new RequestValidationException("Contacto inválido", "Indique um e-mail ou número de telefone válido.");
        }

        string contacto = ContactNormalizer.Normalize(request.Contact!);
        var existente = await _context.Suppressions.FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.Contact == contacto, ct);
        bool jaExistia = existente != null;
        existente ??= new Suppression
        {
            TenantId = request.TenantId,
            Contact = contacto,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            Source = request.Source,
            CreatedBy = actingUser,
            CreatedAt = DateTime.UtcNow
        };
        if (!jaExistia)
        {
            _context.Suppressions.Add(existente);
        }

        var pendentes = (await _context.MessageDispatches
                .Where(m => m.TenantId == request.TenantId
                            && (m.CurrentStatus == DispatchStatus.Held || m.CurrentStatus == DispatchStatus.Queued)
                            && m.RecipientContact.Trim().ToLower() == contacto.ToLower())
                .ToListAsync(ct))
            .Where(m => ContactNormalizer.Normalize(m.RecipientContact) == contacto)
            .ToList();
        foreach (var m in pendentes)
        {
            m.CurrentStatus = DispatchStatus.Suppressed;
            m.ErrorLog = $"Contacto suprimido por {actingUser}";
        }

        await _context.SaveChangesAsync(ct);
        return new SuppressionResultDto { Suppression = ToDto(existente), AlreadyExisted = jaExistia, PendingDispatchesSuppressed = pendentes.Count };
    }

    public async Task<bool> RemoveAsync(int suppressionId, CancellationToken ct = default)
    {
        var s = await _context.Suppressions.FirstOrDefaultAsync(x => x.Id == suppressionId, ct);
        if (s is null)
        {
            return false;
        }
        _context.Suppressions.Remove(s);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static SuppressionDto ToDto(Suppression s) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        Contact = s.Contact,
        Reason = s.Reason,
        Source = s.Source,
        CreatedBy = s.CreatedBy,
        CreatedAt = s.CreatedAt
    };
}
