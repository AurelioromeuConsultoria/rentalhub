using System.Security.Claims;

namespace RentalHub.Application.Security;

public static class PlatformAdminClaims
{
    public static bool IsPlatformAdmin(ClaimsPrincipal? user)
    {
        return string.Equals(
                   user?.FindFirst("IsPlatformAdmin")?.Value,
                   bool.TrueString,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   user?.FindFirst("IsRootTenant")?.Value,
                   bool.TrueString,
                   StringComparison.OrdinalIgnoreCase);
    }
}
