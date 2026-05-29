namespace RentalHub.Domain.Entities;

public interface ITenantEntity
{
    int TenantId { get; set; }
    Tenant? Tenant { get; set; }
}

