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
}