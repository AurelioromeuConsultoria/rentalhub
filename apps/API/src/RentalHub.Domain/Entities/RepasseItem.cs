namespace RentalHub.Domain.Entities;

public sealed class RepasseItem : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int RepasseProprietarioId { get; set; }
    public RepasseProprietario? RepasseProprietario { get; set; }
    public int? ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
    public int? MovimentacaoFinanceiraId { get; set; }
    public MovimentacaoFinanceira? MovimentacaoFinanceira { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Receita { get; set; }
    public decimal Taxas { get; set; }
    public decimal Custos { get; set; }
    public decimal Comissao { get; set; }
    public decimal ValorLiquido { get; set; }
}
