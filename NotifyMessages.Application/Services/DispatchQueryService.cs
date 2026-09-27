using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Entities;

namespace NotifyMessages.Application.Services;

// Histórico de envios para a administração (todos os tenants, com filtros).
public class DispatchQueryService : IDispatchQueryService
{
    private readonly IAppDbContext _context;

    public DispatchQueryService(IAppDbContext context)
    {
        _context = context;
    }

    internal static readonly Expression<Func<MessageDispatch, DispatchListItemDto>> ToListItem = m => new DispatchListItemDto
    {
        Id = m.Id,
        TenantId = m.TenantId,
        TemplateId = m.TemplateId,
        TriggerId = m.TriggerId,
        BatchId = m.BatchId,
        ExternalKey = m.ExternalKey,
        RecipientName = m.RecipientName,
        RecipientContact = m.RecipientContact,
        SentTo = m.SentTo,
        Status = m.CurrentStatus,
        RetryCount = m.RetryCount,
        LastError = m.ErrorLog,
        CreatedAt = m.CreatedAt,
        ProcessedAt = m.ProcessedAt
    };

    public async Task<DispatchHistoryDto> SearchAsync(DispatchQueryDto query, CancellationToken ct = default)
    {
        var (page, pageSize) = BatchService.Paging(query.Page, query.PageSize);
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var filtrado = _context.MessageDispatches.AsNoTracking()
            .Where(m => query.TenantId == null || m.TenantId == query.TenantId)
            .Where(m => query.TriggerId == null || m.TriggerId == query.TriggerId)
            .Where(m => query.BatchId == null || m.BatchId == query.BatchId)
            .Where(m => query.From == null || m.CreatedAt >= query.From)
            .Where(m => query.To == null || m.CreatedAt <= query.To)
            .Where(m => search == null || m.RecipientContact.Contains(search) || m.RecipientName.Contains(search)
                        || (m.ExternalKey != null && m.ExternalKey.Contains(search)));

        // Contagens por estado sem o filtro de estado (para a UI mostrar os separadores)
        var contagens = await filtrado.GroupBy(m => m.CurrentStatus)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        var comEstado = filtrado.Where(m => query.Status == null || m.CurrentStatus == query.Status);
        int total = await comEstado.CountAsync(ct);
        var itens = await comEstado.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ToListItem)
            .ToListAsync(ct);

        return new DispatchHistoryDto
        {
            Items = itens,
            Total = total,
            Page = page,
            PageSize = pageSize,
            CountsByStatus = contagens.ToDictionary(c => c.Status.ToString(), c => c.Total)
        };
    }
}
