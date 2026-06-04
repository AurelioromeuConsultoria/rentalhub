namespace RentalHub.Domain.Entities;

public sealed class LgpdConsent : ITenantEntity
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string TermsVersion { get; set; } = string.Empty;
    public string PrivacyVersion { get; set; } = string.Empty;
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
