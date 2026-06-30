using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class MovimentacaoFinanceira : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public MovimentacaoFinanceiraTipo Tipo { get; set; }
    public int CategoriaFinanceiraId { get; set; }
    public CategoriaFinanceira? CategoriaFinanceira { get; set; }
    public int? ImovelId { get; set; }
    public Imovel? Imovel { get; set; }
    public int? ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
    public int? ProprietarioId { get; set; }
    public Proprietario? Proprietario { get; set; }
    public DateTime Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string? Observacoes { get; set; }
    public string? GrupoRecorrenciaId { get; set; }
    public int? ParcelaAtual { get; set; }
    public int? TotalParcelas { get; set; }
    public MovimentacaoRecorrenciaFrequencia? RecorrenciaFrequencia { get; set; }
    public int? RecorrenciaIntervalo { get; set; }
    public DateTime? RecorrenciaFim { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
