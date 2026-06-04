using System.Security.Claims;
using RentalHub.API.Security;
using RentalHub.Domain.Security;

namespace RentalHub.API.Tests;

public sealed class PermissionMiddlewareTests
{
    [Theory]
    [InlineData("/api/financeiro/fluxo-caixa", "GET", Resources.Financeiro, PermissionAccess.View)]
    [InlineData("/api/financeiro/movimentacoes", "POST", Resources.Financeiro, PermissionAccess.Edit)]
    [InlineData("/api/reservas/10", "DELETE", Resources.Reservas, PermissionAccess.Delete)]
    [InlineData("/api/categoriasfinanceiras", "PUT", Resources.Financeiro, PermissionAccess.Edit)]
    [InlineData("/api/perfis-acesso", "GET", Resources.PerfisAcesso, PermissionAccess.View)]
    [InlineData("/api/configuracoes/tenant", "PUT", Resources.Configuracoes, PermissionAccess.Edit)]
    [InlineData("/api/auditoria", "GET", Resources.Auditoria, PermissionAccess.View)]
    [InlineData("/api/tenants", "GET", Resources.Tenants, PermissionAccess.View)]
    [InlineData("/api/tenants", "POST", Resources.Tenants, PermissionAccess.Edit)]
    public void TryCreateCheck_ShouldResolveKnownApiResources(
        string path,
        string method,
        string expectedResource,
        PermissionAccess expectedAccess)
    {
        var resolved = PermissionMiddleware.TryCreateCheck(path, method, out var check);

        Assert.True(resolved);
        Assert.Equal(expectedResource, check.Resource);
        Assert.Equal(expectedAccess, check.Access);
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/health")]
    [InlineData("/api/notificacoes")]
    [InlineData("/api/buscaglobal")]
    [InlineData("/api/lgpd/status")]
    [InlineData("/")]
    public void TryCreateCheck_ShouldSkipPublicOrAuthenticatedOnlyRoutes(string path)
    {
        var resolved = PermissionMiddleware.TryCreateCheck(path, "GET", out _);

        Assert.False(resolved);
    }

    [Theory]
    [InlineData(PermissionAccess.View, true)]
    [InlineData(PermissionAccess.Edit, false)]
    [InlineData(PermissionAccess.Delete, true)]
    public void HasPermission_ShouldReadPermissionClaim(PermissionAccess access, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("permission", $"{Resources.Financeiro}:true:false:true")
        ], "Test"));

        var allowed = PermissionMiddleware.HasPermission(user, new PermissionCheck(Resources.Financeiro, access));

        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void ResourceCatalog_ShouldExposeDistinctAdministrativeResources()
    {
        Assert.Contains(Resources.Tenants, Resources.All);
        Assert.Contains(Resources.Configuracoes, Resources.All);
        Assert.Equal(Resources.All.Length, Resources.All.Distinct(StringComparer.Ordinal).Count());
    }
}
