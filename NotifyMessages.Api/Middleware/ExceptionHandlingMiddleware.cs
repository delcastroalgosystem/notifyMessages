using System.Net;
using NotifyMessages.Application.Exceptions;

namespace NotifyMessages.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DuplicateDispatchException ex)
        {
            _logger.LogWarning("Pedido de disparo duplicado rejeitado (envio existente {DispatchId})", ex.ExistingDispatchId);
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Pedido duplicado",
                status = (int)HttpStatusCode.Conflict,
                detail = ex.Message,
                dispatchId = ex.ExistingDispatchId
            });
        }
        catch (DuplicateBatchException ex)
        {
            _logger.LogWarning("Lote repetido rejeitado (lote existente {BatchId})", ex.ExistingBatchId);
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Lote repetido",
                status = (int)HttpStatusCode.Conflict,
                detail = ex.Message,
                batchId = ex.ExistingBatchId
            });
        }
        catch (RequestValidationException ex)
        {
            _logger.LogWarning("Pedido inválido: {Title} - {Detail}", ex.Title, ex.Message);
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, ex.Title, ex.Message);
        }
        catch (TemplateNotAvailableException ex)
        {
            _logger.LogWarning("Pedido com template indisponível para o tenant: {TemplateId}", ex.TemplateId);
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Template inválido", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção não tratada ao processar {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "Erro interno", "Ocorreu um erro inesperado ao processar o pedido.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, HttpStatusCode statusCode, string title, string detail)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsJsonAsync(new
        {
            title,
            status = (int)statusCode,
            detail
        });
    }
}
