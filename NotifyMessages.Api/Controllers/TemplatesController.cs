using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;

namespace NotifyMessages.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _service;
    private readonly IValidator<TemplateUpsertDto> _validator;

    public TemplatesController(ITemplateService service, IValidator<TemplateUpsertDto> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, CancellationToken ct)
        => Ok(await _service.GetAllAsync(TenantId, isActive, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var template = await _service.GetByIdAsync(TenantId, id, ct);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TemplateUpsertDto request, CancellationToken ct)
    {
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var created = await _service.CreateAsync(TenantId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TemplateUpsertDto request, CancellationToken ct)
    {
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        var updated = await _service.UpdateAsync(TenantId, id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var success = await _service.DeactivateAsync(TenantId, id, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/preview")]
    public async Task<IActionResult> Preview(int id, [FromBody] TemplatePreviewRequestDto request, CancellationToken ct)
    {
        var result = await _service.PreviewAsync(TenantId, id, request, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // Templates do tenant autenticado pela X-Api-Key (e os partilhados, só para leitura).
    private int TenantId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
