using RentalHub.Domain.Entities;

namespace RentalHub.API.Tests;

public sealed class TenantFoundationTests
{
    [Fact]
    public void InitialTenantValues_ShouldMatchRentalHubFoundation()
    {
        Assert.Equal(1, Tenant.InitialTenantId);
        Assert.Equal("RentalHub", Tenant.InitialTenantName);
        Assert.Equal("rentalhub", Tenant.InitialTenantSlug);
    }
}
