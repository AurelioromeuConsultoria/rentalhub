using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class Limpeza : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ImovelId { get; set; }
    public Imovel? Imovel { get; set; }
    public int? ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
    public DateTime DataPrevista { get; set; }
    public string Responsavel { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public LimpezaStatus Status { get; set; } = LimpezaStatus.Pendente;
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
