using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class CategoriaFinanceira : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Nome { get; set; } = string.Empty;
    public MovimentacaoFinanceiraTipo Tipo { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
    public ICollection<MovimentacaoFinanceira> Movimentacoes { get; set; } = [];
}
