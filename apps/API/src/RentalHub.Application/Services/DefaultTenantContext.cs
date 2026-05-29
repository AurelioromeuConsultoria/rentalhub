using RentalHub.Domain.Entities;

namespace RentalHub.Application.Services;

public sealed class DefaultTenantContext : ITenantContext
{
    public int? TenantId => Tenant.InitialTenantId;
    public string? TenantSlug => Tenant.InitialTenantSlug;
}

