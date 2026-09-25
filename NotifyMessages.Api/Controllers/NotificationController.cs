using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;

namespace NotifyMessages.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
	private readonly IDispatchService _service;
	private readonly IValidator<DispatchRequestDto> _validator;

	public NotificationController(IDispatchService service, IValidator<DispatchRequestDto> validator)
	{
		_service = service;
		_validator = validator;
	}

	[HttpPost]
	public async Task<IActionResult> SendNotification([FromBody] DispatchRequestDto request)
	{
		var validationResult = await _validator.ValidateAsync(request);
		if (!validationResult.IsValid)
		{
			return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
		}

		if (request.TenantId != AuthenticatedTenantId)
		{
			return Problem(
				statusCode: StatusCodes.Status403Forbidden,
				title: "Tenant não autorizado",
				detail: "O TenantId do pedido não corresponde ao Tenant autenticado pela API Key.");
		}

		// Exceções (ex: duplicidade, erros inesperados) são tratadas pelo ExceptionHandlingMiddleware
		var trackingId = await _service.EnqueueMessageAsync(request);

		// Retorna 202 Accepted (pois foi para a fila, não enviado ainda); TrackingId = Id do MessageDispatch
		return AcceptedAtAction(nameof(GetNotification), new { id = trackingId }, new
		{
			Message = "Notificação enfileirada com sucesso.",
			TrackingId = trackingId
		});
	}

	[HttpGet("{id:long}")]
	public async Task<IActionResult> GetNotification(long id, CancellationToken ct)
	{
		// Envios de outro tenant respondem 404, para não revelar que existem
		var status = await _service.GetStatusAsync(id, AuthenticatedTenantId, ct);
		return status == null ? NotFound() : Ok(status);
	}

	private int AuthenticatedTenantId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}