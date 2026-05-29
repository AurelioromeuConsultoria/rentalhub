namespace RentalHub.Application.Services;

public interface ITenantContext
{
    int? TenantId { get; }
    string? TenantSlug { get; }
}

