using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;

namespace NotifyMessages.Api.Controllers;

// Lotes de envios: entregues por um conector (com TriggerId) ou pelo software do cliente (com TemplateId).
[ApiController]
[Route("api/v1/batches")]
[Authorize]
public class BatchesController : ControllerBase
{
    private readonly IBatchService _service;

    public BatchesController(IBatchService service)
    {
        _service = service;
    }

    // 201 com as contagens; 409 (com batchId) se o gatilho já tem lote para esse dia; 400 se inválido.
    // Exceções tratadas pelo ExceptionHandlingMiddleware.
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BatchCreateDto request, CancellationToken ct)
    {
        var batch = await _service.CreateAsync(TenantId, request, createdBy: User.Identity?.Name, ct);
        // 204: gatilho de intervalo sem nada de novo — a execução ficou registada, mas não há lote
        return batch is null ? NoContent() : CreatedAtAction(nameof(Get), new { id = batch.Id }, batch);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var batch = await _service.GetAsync(TenantId, id, ct);
        return batch is null ? NotFound() : Ok(batch);
    }

    private int TenantId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
