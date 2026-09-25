using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotifyMessages.Application.Interfaces;

namespace NotifyMessages.Api.Controllers;

// API do conector: "que gatilhos do meu tenant tenho de correr agora?" (modelo pull).
[ApiController]
[Route("api/v1/triggers")]
[Authorize]
public class TriggersController : ControllerBase
{
    private readonly ITriggerService _service;
    private readonly IHostEnvironment _environment;

    public TriggersController(ITriggerService service, IHostEnvironment environment)
    {
        _service = service;
        _environment = environment;
    }

    // at (opcional, UTC): "como se fosse" este instante - só fora de Produção, para testar agendas.
    [HttpGet("due")]
    public async Task<IActionResult> GetDue([FromQuery] DateTime? at, CancellationToken ct)
    {
        if (at.HasValue && _environment.IsProduction())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Parâmetro não permitido",
                detail: "'at' (data simulada) só é aceite fora de Produção.");
        }

        var agora = at.HasValue ? DateTime.SpecifyKind(at.Value, DateTimeKind.Utc) : DateTime.UtcNow;
        return Ok(await _service.GetDueAsync(TenantId, agora, ct));
    }

    private int TenantId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
