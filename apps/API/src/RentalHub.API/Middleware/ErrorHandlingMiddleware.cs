using System.Security.Claims;

namespace RentalHub.API.Middleware;

public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });

        try
        {
            await _next(context);

            if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                LogServerError(context, null);
            }
        }
        catch (Exception ex)
        {
            LogServerError(context, ex);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                message = "Erro interno no servidor.",
                traceId = context.TraceIdentifier
            });
        }
    }

    private void LogServerError(HttpContext context, Exception? exception)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantId = context.User.FindFirstValue("TenantId") ??
                       context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        _logger.LogError(
            exception,
            "HTTP {StatusCode} em {Method} {Path}. TraceId={TraceId}; TenantId={TenantId}; UserId={UserId}; RemoteIp={RemoteIp}",
            context.Response.StatusCode,
            context.Request.Method,
            context.Request.Path.Value,
            context.TraceIdentifier,
            tenantId,
            userId,
            context.Connection.RemoteIpAddress?.ToString());
    }
}
