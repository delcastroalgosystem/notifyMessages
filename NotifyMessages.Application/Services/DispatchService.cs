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
		// 1. Serializa o objeto de negócio para string JSON
		string jsonContext = JsonSerializer.Serialize(request.BusinessData);

		// 2. Gera a Chave de Idempotência (Anti-Duplicidade)
		// A chave é: Tenant + Template + Contato + Dados
		string rawKey = $"{request.TenantId}-{request.TemplateId}-{request.RecipientContact}-{jsonContext}";
		string idempotencyKey = GenerateHash(rawKey);

		// 3. Verifica se já existe (o índice único também barra; aqui devolve o Id do envio existente)
		var existingId = await FindByIdempotencyKeyAsync(idempotencyKey);
		if (existingId.HasValue)
		{
			throw new DuplicateDispatchException(existingId.Value);
		}

		// 4. Cria a Entidade
		var message = new MessageDispatch
		{
			TenantId = request.TenantId,
			TemplateId = request.TemplateId,
			RecipientName = request.RecipientName,
			RecipientContact = request.RecipientContact,
			IdempotencyKey = idempotencyKey,
			CurrentStatus = DispatchStatus.Queued // <--- Entra como FILA
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
			var concurrentId = await FindByIdempotencyKeyAsync(idempotencyKey);
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
			RetryCount = dispatch.RetryCount,
			LastError = dispatch.ErrorLog,
			CreatedAt = dispatch.CreatedAt,
			ProcessedAt = dispatch.ProcessedAt,
			Events = events
		};
	}

	private async Task<long?> FindByIdempotencyKeyAsync(string idempotencyKey)
		=> await _context.MessageDispatches
			.AsNoTracking()
			.Where(x => x.IdempotencyKey == idempotencyKey)
			.Select(x => (long?)x.Id)
			.FirstOrDefaultAsync();

	private static string GenerateHash(string input)
	{
		using var sha256 = SHA256.Create();
		var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(bytes);
	}
}