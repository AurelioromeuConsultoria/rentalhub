using System.Security.Claims;
using RentalHub.Application.Services;

namespace RentalHub.API.Services;

public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public int? UserId => int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : null;

    public string? UserName => User?.FindFirstValue(ClaimTypes.Name);
    public string? UserEmail => User?.FindFirstValue(ClaimTypes.Email);
    public bool IsPlatformAdmin => string.Equals(
        User?.FindFirstValue("IsPlatformAdmin"),
        "true",
        StringComparison.OrdinalIgnoreCase);
}

