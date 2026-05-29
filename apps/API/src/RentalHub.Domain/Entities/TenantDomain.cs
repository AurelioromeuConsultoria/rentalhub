namespace RentalHub.Domain.Entities;

public sealed class TenantDomain : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Domain { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool Ativo { get; set; } = true;
}
