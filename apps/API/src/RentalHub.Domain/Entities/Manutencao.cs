using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class Manutencao : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ImovelId { get; set; }
    public Imovel? Imovel { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Responsavel { get; set; }
    public DateTime DataAbertura { get; set; } = DateTime.UtcNow;
    public DateTime? DataPrevista { get; set; }
    public DateTime? DataResolucao { get; set; }
    public decimal ValorEstimado { get; set; }
    public decimal ValorRealizado { get; set; }
    public ManutencaoStatus Status { get; set; } = ManutencaoStatus.Aberta;
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
