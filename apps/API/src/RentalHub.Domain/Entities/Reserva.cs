using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class Reserva : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ImovelId { get; set; }
    public Imovel? Imovel { get; set; }
    public int HospedeId { get; set; }
    public Hospede? Hospede { get; set; }
    public ReservaOrigem Origem { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int NumeroHospedes { get; set; }
    public decimal ValorHospedagem { get; set; }
    public decimal TaxaLimpeza { get; set; }
    public decimal TaxaPlataforma { get; set; }
    public decimal ComissaoAdministradora { get; set; }
    public decimal ValorLiquido { get; set; }
    public ReservaStatus Status { get; set; } = ReservaStatus.Pendente;
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
