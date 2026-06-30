using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RentalHub.API.Services;
using RentalHub.Domain.Entities;

namespace RentalHub.API.Tests;

public sealed class HttpTenantContextTests
{
    [Fact]
    public void TenantId_ShouldIgnoreOverrideHeader_WhenUserIsNotPlatformAdmin()
    {
        var context = CreateContext(
            isPlatformAdmin: false,
            tenantId: 10,
            tenantSlug: "empresa-a");
        context.Request.Headers["X-Tenant-Id"] = "20";
        context.Request.Headers["X-Tenant-Slug"] = "empresa-b";

        var tenantContext = new HttpTenantContext(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(10, tenantContext.TenantId);
        Assert.Equal("empresa-a", tenantContext.TenantSlug);
    }

    [Fact]
    public void TenantId_ShouldUseOverrideHeader_WhenUserIsPlatformAdmin()
    {
        var context = CreateContext(
            isPlatformAdmin: true,
            tenantId: Tenant.InitialTenantId,
            tenantSlug: Tenant.InitialTenantSlug);
        context.Request.Headers["X-Tenant-Id"] = "20";
        context.Request.Headers["X-Tenant-Slug"] = "empresa-b";

        var tenantContext = new HttpTenantContext(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(20, tenantContext.TenantId);
        Assert.Equal("empresa-b", tenantContext.TenantSlug);
    }

    [Fact]
    public void TenantId_ShouldFallbackToTokenTenant_WhenPlatformAdminHeaderIsInvalid()
    {
        var context = CreateContext(
            isPlatformAdmin: true,
            tenantId: Tenant.InitialTenantId,
            tenantSlug: Tenant.InitialTenantSlug);
        context.Request.Headers["X-Tenant-Id"] = "invalido";

        var tenantContext = new HttpTenantContext(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(Tenant.InitialTenantId, tenantContext.TenantId);
    }

    [Fact]
    public void TenantId_ShouldIgnoreOverrideHeader_WhenPlatformFlagIsOnNonRootTenant()
    {
        var context = CreateContext(
            isPlatformAdmin: true,
            tenantId: 10,
            tenantSlug: "empresa-a",
            isRootTenant: false);
        context.Request.Headers["X-Tenant-Id"] = "20";

        var tenantContext = new HttpTenantContext(new HttpContextAccessor { HttpContext = context });

        Assert.Equal(10, tenantContext.TenantId);
    }

    private static DefaultHttpContext CreateContext(
        bool isPlatformAdmin,
        int tenantId,
        string tenantSlug,
        bool isRootTenant = true)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("TenantId", tenantId.ToString()),
            new Claim("TenantSlug", tenantSlug),
            new Claim("IsPlatformAdmin", isPlatformAdmin.ToString().ToLowerInvariant()),
            new Claim("IsRootTenant", isRootTenant.ToString().ToLowerInvariant())
        ], "Test"));

        return context;
    }
}
