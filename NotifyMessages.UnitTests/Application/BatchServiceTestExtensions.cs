using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Services;

namespace NotifyMessages.UnitTests.Application;

internal static class BatchServiceTestExtensions
{
    // Lote de um gatilho diário ou da API: há sempre lote (o nulo é só dos gatilhos de intervalo sem nada de novo).
    public static async Task<BatchDto> CriarAsync(this BatchService service, int tenantId, BatchCreateDto request)
        => await service.CreateAsync(tenantId, request) ?? throw new InvalidOperationException("Lote nulo inesperado.");
}
