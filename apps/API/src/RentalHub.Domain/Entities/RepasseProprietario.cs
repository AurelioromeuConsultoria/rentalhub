using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class RepasseProprietario : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ProprietarioId { get; set; }
    public Proprietario? Proprietario { get; set; }
    public int? ImovelId { get; set; }
    public Imovel? Imovel { get; set; }
    public DateTime PeriodoInicio { get; set; }
    public DateTime PeriodoFim { get; set; }
    public decimal ReceitaReservas { get; set; }
    public decimal TaxasPlataforma { get; set; }
    public decimal CustosVinculados { get; set; }
    public decimal ComissaoAdministradora { get; set; }
    public decimal ValorRepassar { get; set; }
    public decimal ValorPago { get; set; }
    public RepasseStatus Status { get; set; } = RepasseStatus.Pendente;
    public DateTime? DataPagamento { get; set; }
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
    public List<RepasseItem> Itens { get; set; } = [];
}
