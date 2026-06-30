using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class ReservaVeiculoCadastro : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ReservaPreCheckinId { get; set; }
    public ReservaPreCheckin? ReservaPreCheckin { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Cor { get; set; }
    public string? Observacoes { get; set; }
    public PreCheckinItemStatus Status { get; set; } = PreCheckinItemStatus.Pendente;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
