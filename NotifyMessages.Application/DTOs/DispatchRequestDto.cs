using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace NotifyMessages.Application.DTOs;

public class DispatchRequestDto
{
	public int TenantId { get; set; } // Quem está enviando?
	public int TemplateId { get; set; } // Qual mensagem enviar?

	public required string RecipientName { get; set; }
	public required string RecipientContact { get; set; } // Email ou Celular

	// O "Payload" genérico. O usuário manda um objeto JSON qualquer aqui.
	// Ex: { "valor": 100, "vencimento": "2026-05-20" }
	public object? BusinessData { get; set; }

	// Opcional: chave de idempotência do cliente (ex. "SOCIO:1234:2026"). Quando vem, a unicidade é
	// por (tenant, chave) - sem depender dos dados. Sem ela, mantém-se o hash tenant+template+contacto+dados.
	public string? ExternalKey { get; set; }

	// Opcional: não enviar antes desta data/hora (UTC).
	public DateTime? ScheduledAt { get; set; }
}