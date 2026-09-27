using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Domain.Enums;

namespace NotifyMessages.Api.Controllers.Admin;

// API de administração (README, "API de administração"). Exceções de validação → ExceptionHandlingMiddleware (400/409).

[Route("api/v1/admin/tenants")]
public class AdminTenantsController(ITenantAdminService service) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpPut("{id:int}/settings")]
    public async Task<IActionResult> UpdateSettings(int id, [FromBody] TenantSettingsDto settings, CancellationToken ct)
    {
        var tenant = await service.UpdateSettingsAsync(id, settings, ct);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}

[Route("api/v1/admin/templates")]
public class AdminTemplatesController(ITemplateService service, IValidator<TemplateUpsertDto> validator) : AdminControllerBase
{
    // tenantId: os templates desse tenant e os partilhados; sem filtro: todos.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? tenantId, [FromQuery] bool? isActive, CancellationToken ct)
        => Ok((await service.GetAllAsync(null, isActive, ct)).Where(t => tenantId == null || t.TenantId == tenantId || t.TenantId == null));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var template = await service.GetByIdAsync(null, id, ct);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminTemplateUpsertDto request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }
        var created = await service.CreateAsync(request.TenantId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TemplateUpsertDto request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }
        var updated = await service.UpdateAsync(null, id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
        => await service.DeactivateAsync(null, id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:int}/preview")]
    public async Task<IActionResult> Preview(int id, [FromBody] TemplatePreviewRequestDto request, CancellationToken ct)
    {
        var result = await service.PreviewAsync(null, id, request, ct);
        return result is null ? NotFound() : Ok(result);
    }
}

[Route("api/v1/admin/triggers")]
public class AdminTriggersController(ITriggerService service) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? tenantId, CancellationToken ct) => Ok(await service.ListAsync(tenantId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var trigger = await service.GetAsync(id, ct);
        return trigger is null ? NotFound() : Ok(trigger);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TriggerUpsertDto request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ActingUser, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    // Desativar = IsActive false (não há apagar: os lotes e envios referem o gatilho).
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] TriggerUpsertDto request, CancellationToken ct)
    {
        var updated = await service.UpdateAsync(id, request, ActingUser, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}

[Route("api/v1/admin/batches")]
public class AdminBatchesController(IBatchService service, IBatchFileParser parser) : AdminControllerBase
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] BatchQueryDto query, CancellationToken ct) => Ok(await service.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var batch = await service.GetAsync(null, id, ct);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpGet("{id:long}/items")]
    public async Task<IActionResult> Items(long id, [FromQuery] DispatchStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var items = await service.ItemsAsync(id, status, page, pageSize, ct);
        return items is null ? NotFound() : Ok(items);
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken ct)
    {
        var batch = await service.ApproveAsync(id, ActingUser, ct);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        var batch = await service.CancelAsync(id, ActingUser, ct);
        return batch is null ? NotFound() : Ok(batch);
    }

    // multipart/form-data: tenantId, triggerId (gatilho, ex. de tipo FILE) ou templateId, file (.csv/.xlsx).
    // O lote fica sempre à espera de aprovação.
    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Upload([FromForm] int tenantId, [FromForm] int? triggerId, [FromForm] int? templateId, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ficheiro em falta", detail: "Envie o ficheiro no campo 'file'.");
        }
        if (file.Length > MaxFileBytes)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ficheiro demasiado grande", detail: $"Máximo {MaxFileBytes / 1024 / 1024} MB.");
        }

        await using var stream = file.OpenReadStream();
        var parsed = parser.Parse(stream, file.FileName);
        if (parsed.Items.Count == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Ficheiro sem linhas válidas",
                detail: string.Join(" ", parsed.Errors.DefaultIfEmpty("Nenhuma linha de dados encontrada.")));
        }

        var batch = await service.CreateFromFileAsync(tenantId, triggerId, templateId, Path.GetFileName(file.FileName), parsed, ActingUser, ct);
        return CreatedAtAction(nameof(Get), new { id = batch.Id }, batch);
    }
}

[Route("api/v1/admin/suppressions")]
public class AdminSuppressionsController(ISuppressionService service) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? tenantId, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await service.ListAsync(tenantId, search, page, pageSize, ct));

    // 201 novo; 200 se o contacto já estava suprimido.
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SuppressionCreateDto request, CancellationToken ct)
    {
        var result = await service.AddAsync(request, ActingUser, ct);
        return result.AlreadyExisted ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken ct) => await service.RemoveAsync(id, ct) ? NoContent() : NotFound();
}

[Route("api/v1/admin/dispatches")]
public class AdminDispatchesController(IDispatchQueryService service) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] DispatchQueryDto query, CancellationToken ct) => Ok(await service.SearchAsync(query, ct));
}
