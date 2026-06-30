using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Entities;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Middleware;

public sealed class PlatformSupportAccessMiddleware
{
    public const string HeaderToken = "X-Support-Access-Token";

    private readonly RequestDelegate _next;

    public PlatformSupportAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, RentalHubDbContext dbContext)
    {
        if (!ShouldValidate(context, out var selectedTenantId, out var userId))
        {
            await _next(context);
            return;
        }

        var token = context.Request.Headers[HeaderToken].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            await WriteForbiddenAsync(context, "Acesso aos dados do cliente exige modo suporte com motivo registrado.");
            return;
        }

        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;
        var session = await dbContext.SupportAccessSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(session =>
                session.TenantId == selectedTenantId &&
                session.UsuarioId == userId &&
                session.TokenHash == tokenHash &&
                session.EncerradoEm == null &&
                session.ExpiraEm > now,
                context.RequestAborted);

        if (session is null)
        {
            await WriteForbiddenAsync(context, "Sessão de suporte inválida ou expirada. Informe novamente o motivo do acesso.");
            return;
        }

        await _next(context);
        await RecordSupportRouteAccessAsync(context, dbContext, session);
    }

    private static bool ShouldValidate(HttpContext context, out int selectedTenantId, out int userId)
    {
        selectedTenantId = 0;
        userId = 0;

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/api/support-access", StringComparison.OrdinalIgnoreCase) ||
            context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var isPlatformAdmin = string.Equals(
            context.User.FindFirstValue("IsPlatformAdmin"),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

        if (!isPlatformAdmin ||
            !int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) ||
            !int.TryParse(context.User.FindFirstValue("TenantId"), out var ownTenantId) ||
            !context.Request.Headers.TryGetValue("X-Tenant-Id", out var selectedTenantHeader) ||
            !int.TryParse(selectedTenantHeader.ToString(), out selectedTenantId))
        {
            return false;
        }

        return selectedTenantId != ownTenantId;
    }

    private static Task WriteForbiddenAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new { message });
    }

    private static async Task RecordSupportRouteAccessAsync(
        HttpContext context,
        RentalHubDbContext dbContext,
        SupportAccessSession session)
    {
        var path = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = session.TenantId,
            EntityName = "SuporteAcesso",
            EntityId = Truncate(path, 80),
            Action = Truncate($"Suporte {context.Request.Method} {context.Response.StatusCode}", 40),
            UserName = context.User.FindFirstValue(ClaimTypes.Name),
            UserEmail = context.User.FindFirstValue(ClaimTypes.Email),
            IpAddress = GetIpAddress(context),
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(context.RequestAborted);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? GetIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}

public static class PlatformSupportAccessMiddlewareExtensions
{
    public static IApplicationBuilder UsePlatformSupportAccess(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PlatformSupportAccessMiddleware>();
    }
}
