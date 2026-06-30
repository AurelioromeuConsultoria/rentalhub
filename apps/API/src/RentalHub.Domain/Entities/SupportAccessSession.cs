namespace RentalHub.Domain.Entities;

public sealed class SupportAccessSession : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
    public DateTime? EncerradoEm { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}
