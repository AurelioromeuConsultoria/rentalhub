using System.Security.Claims;
using RentalHub.Domain.Security;

namespace RentalHub.API.Security;

public enum PermissionAccess
{
    View,
    Edit,
    Delete
}

public sealed record PermissionCheck(string Resource, PermissionAccess Access);

public sealed class PermissionMiddleware
{
    private static readonly HashSet<string> PublicApiSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "auth",
        "health"
    };

    private static readonly HashSet<string> AuthenticatedOnlySegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "buscaglobal",
        "notificacoes",
        "portalproprietario",
        "lgpd",
        "sistema",
        "suporte"
    };

    private static readonly Dictionary<string, string> ResourceBySegment = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = Resources.Dashboard,
        ["imoveis"] = Resources.Imoveis,
        ["proprietarios"] = Resources.Proprietarios,
        ["hospedes"] = Resources.Hospedes,
        ["reservas"] = Resources.Reservas,
        ["calendario"] = Resources.Calendario,
        ["financeiro"] = Resources.Financeiro,
        ["categoriasfinanceiras"] = Resources.Financeiro,
        ["repasses"] = Resources.Repasses,
        ["limpezas"] = Resources.Limpezas,
        ["manutencoes"] = Resources.Manutencoes,
        ["relatorios"] = Resources.Relatorios,
        ["usuarios"] = Resources.Usuarios,
        ["perfis-acesso"] = Resources.PerfisAcesso,
        ["perfisacesso"] = Resources.PerfisAcesso,
        ["tenants"] = Resources.Tenants,
        ["configuracoes"] = Resources.Configuracoes,
        ["auditoria"] = Resources.Auditoria
    };

    private readonly RequestDelegate _next;

    public PermissionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!TryCreateCheck(context.Request.Path, context.Request.Method, out var check))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (IsPlatformAdmin(context.User) || HasPermission(context.User, check))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Você não possui permissão para acessar este recurso."
        });
    }

    public static bool TryCreateCheck(PathString path, string method, out PermissionCheck check)
    {
        check = default!;

        if (!path.StartsWithSegments("/api", out var remaining))
        {
            return false;
        }

        var segment = remaining.Value?
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(segment) ||
            PublicApiSegments.Contains(segment) ||
            AuthenticatedOnlySegments.Contains(segment))
        {
            return false;
        }

        if (!ResourceBySegment.TryGetValue(segment, out var resource) ||
            !TryGetAccess(method, out var access))
        {
            return false;
        }

        check = new PermissionCheck(resource, access);
        return true;
    }

    public static bool HasPermission(ClaimsPrincipal user, PermissionCheck check)
    {
        return user.FindAll("permission").Any(claim =>
        {
            var parts = claim.Value.Split(':');
            if (parts.Length != 4 ||
                !string.Equals(parts[0], check.Resource, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return check.Access switch
            {
                PermissionAccess.View => bool.TryParse(parts[1], out var canView) && canView,
                PermissionAccess.Edit => bool.TryParse(parts[2], out var canEdit) && canEdit,
                PermissionAccess.Delete => bool.TryParse(parts[3], out var canDelete) && canDelete,
                _ => false
            };
        });
    }

    private static bool IsPlatformAdmin(ClaimsPrincipal user)
    {
        return string.Equals(
            user.FindFirstValue("IsPlatformAdmin"),
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetAccess(string method, out PermissionAccess access)
    {
        access = PermissionAccess.View;

        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
        {
            return true;
        }

        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method))
        {
            access = PermissionAccess.Edit;
            return true;
        }

        if (HttpMethods.IsDelete(method))
        {
            access = PermissionAccess.Delete;
            return true;
        }

        return false;
    }
}

public static class PermissionMiddlewareExtensions
{
    public static IApplicationBuilder UseRentalHubPermissions(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PermissionMiddleware>();
    }
}
