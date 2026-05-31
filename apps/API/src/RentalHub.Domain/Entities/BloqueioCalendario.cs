using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class BloqueioCalendario : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ImovelId { get; set; }
    public Imovel? Imovel { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public BloqueioCalendarioTipo Tipo { get; set; } = BloqueioCalendarioTipo.Bloqueio;
    public string Motivo { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
