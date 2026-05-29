namespace RentalHub.Domain.Entities;

public sealed class AuditLog : ITenantEntity
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

