using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Exceptions;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Entities;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Application.Services;

public class DispatchService : IDispatchService
{
	private readonly IAppDbContext _context;

	public DispatchService(IAppDbContext context)
	{
		_context = context;
	}

	public async Task<long> EnqueueMessageAsync(DispatchRequestDto request)
	{
		// 1. O template tem de ser do tenant ou partilhado (senão um tenant enviava com o remetente de outro)
		bool templateDisponivel = await _context.Templates.AnyAsync(t =>
			t.Id == request.TemplateId && (t.TenantId == request.TenantId || t.TenantId == null));
		if (!templateDisponivel)
		{
			throw new TemplateNotAvailableException(request.TemplateId);
		}

		// 2. Chave de Idempotência (Anti-Duplicidade)
		// Com ExternalKey: Tenant + chave do cliente. Sem ela: Tenant + Template + Contato + Dados.
		string? externalKey = string.IsNullOrWhiteSpace(request.ExternalKey) ? null : request.ExternalKey.Trim();
		string idempotencyKey = externalKey != null
			? IdempotencyKeys.ForExternalKey(request.TenantId, externalKey)
			: IdempotencyKeys.ForContent(request.TenantId, request.TemplateId, request.RecipientContact, request.BusinessData);

		// 3. Verifica se já existe (os índices únicos também barram; aqui devolve o Id do envio existente)
		var existingId = await FindExistingAsync(idempotencyKey, request.TenantId, externalKey);
		if (existingId.HasValue)
		{
			throw new DuplicateDispatchException(existingId.Value);
		}

		// 4. Contacto em supressão: fica registado como Suppressed e nunca é enviado
		string contactoNormalizado = ContactNormalizer.Normalize(request.RecipientContact);
		bool suprimido = await _context.Suppressions.AnyAsync(s =>
			s.TenantId == request.TenantId && s.Contact == contactoNormalizado);

		// 5. Cria a Entidade
		var message = new MessageDispatch
		{
			TenantId = request.TenantId,
			TemplateId = request.TemplateId,
			RecipientName = request.RecipientName,
			RecipientContact = request.RecipientContact,
			IdempotencyKey = idempotencyKey,
			ExternalKey = externalKey,
			ScheduledAt = request.ScheduledAt,
			CurrentStatus = suprimido ? DispatchStatus.Suppressed : DispatchStatus.Queued // <--- Entra como FILA
		};

		// Salva o JSON dentro da entidade
		message.SetContextData(request.BusinessData);

		_context.MessageDispatches.Add(message);
		try
		{
			await _context.SaveChangesAsync();
		}
		catch (DbUpdateException)
		{
			// Dois pedidos idênticos em simultâneo: o índice único barra o segundo -> 409, não 500
			var concurrentId = await FindExistingAsync(idempotencyKey, request.TenantId, externalKey);
			if (concurrentId.HasValue)
			{
				throw new DuplicateDispatchException(concurrentId.Value);
			}
			throw;
		}

		// O Id do MessageDispatch é o identificador de rastreio (consultável em GET /api/v1/notifications/{id})
		return message.Id;
	}

	public async Task<DispatchStatusDto?> GetStatusAsync(long dispatchId, int tenantId, CancellationToken ct = default)
	{
		var dispatch = await _context.MessageDispatches
			.AsNoTracking()
			.FirstOrDefaultAsync(m => m.Id == dispatchId && m.TenantId == tenantId, ct);

		if (dispatch == null)
		{
			return null;
		}

		var events = await _context.MessageDispatchEvents
			.AsNoTracking()
			.Where(e => e.DispatchId == dispatchId)
			.OrderBy(e => e.EventDate)
			.Select(e => new DispatchEventDto { EventType = e.EventType, EventDate = e.EventDate })
			.ToListAsync(ct);

		return new DispatchStatusDto
		{
			Id = dispatch.Id,
			TenantId = dispatch.TenantId,
			TemplateId = dispatch.TemplateId,
			RecipientContact = dispatch.RecipientContact,
			Status = dispatch.CurrentStatus,
			ExternalId = dispatch.ExternalId,
			ExternalKey = dispatch.ExternalKey,
			BatchId = dispatch.BatchId,
			ScheduledAt = dispatch.ScheduledAt,
			SentTo = dispatch.SentTo,
			RetryCount = dispatch.RetryCount,
			LastError = dispatch.ErrorLog,
			CreatedAt = dispatch.CreatedAt,
			ProcessedAt = dispatch.ProcessedAt,
			Events = events
		};
	}

	private async Task<long?> FindExistingAsync(string idempotencyKey, int tenantId, string? externalKey)
		=> await _context.MessageDispatches
			.AsNoTracking()
			.Where(x => x.IdempotencyKey == idempotencyKey
				|| (externalKey != null && x.TenantId == tenantId && x.ExternalKey == externalKey))
			.Select(x => (long?)x.Id)
			.FirstOrDefaultAsync();
}