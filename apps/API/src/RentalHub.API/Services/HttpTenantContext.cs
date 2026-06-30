using System.Security.Claims;
using RentalHub.Application.Security;
using RentalHub.Application.Services;
using RentalHub.Domain.Entities;

namespace RentalHub.API.Services;

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return Tenant.InitialTenantId;
            }

            var isPlatformAdmin = PlatformAdminClaims.IsPlatformAdmin(httpContext.User);

            if (isPlatformAdmin &&
                httpContext.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue) &&
                int.TryParse(headerValue.ToString(), out var selectedTenantId))
            {
                return selectedTenantId;
            }

            return int.TryParse(httpContext.User.FindFirstValue("TenantId"), out var tenantId)
                ? tenantId
                : Tenant.InitialTenantId;
        }
    }

    public string? TenantSlug
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return Tenant.InitialTenantSlug;
            }

            var isPlatformAdmin = PlatformAdminClaims.IsPlatformAdmin(httpContext.User);

            if (isPlatformAdmin &&
                httpContext.Request.Headers.TryGetValue("X-Tenant-Slug", out var headerValue))
            {
                return headerValue.ToString();
            }

            return httpContext.User.FindFirstValue("TenantSlug") ?? Tenant.InitialTenantSlug;
        }
    }
}
