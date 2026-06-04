namespace RentalHub.Domain.Entities;

public sealed class SupportTicket : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int? CreatedByUsuarioId { get; set; }
    public Usuario? CreatedByUsuario { get; set; }
    public string CreatedByNome { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Prioridade { get; set; } = "media";
    public string Status { get; set; } = "aberto";
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
    public DateTime? DataResolucao { get; set; }
}
