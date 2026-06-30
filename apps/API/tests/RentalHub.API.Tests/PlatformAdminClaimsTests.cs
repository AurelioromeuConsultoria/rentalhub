using System.Security.Claims;
using RentalHub.Application.Security;

namespace RentalHub.API.Tests;

public sealed class PlatformAdminClaimsTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void IsPlatformAdmin_ShouldRequireFlagAndRootTenant(
        bool platformFlag,
        bool rootTenant,
        bool expected)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("IsPlatformAdmin", platformFlag.ToString().ToLowerInvariant()),
            new Claim("IsRootTenant", rootTenant.ToString().ToLowerInvariant())
        ]);
        var user = new ClaimsPrincipal(identity);

        Assert.Equal(expected, PlatformAdminClaims.IsPlatformAdmin(user));
    }
}
