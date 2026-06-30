using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class ConfiguracaoRelatorioMensal : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Nome { get; set; } = string.Empty;
    public ConfiguracaoRelatorioMensalTipoValor TipoValor { get; set; }
    public decimal Valor { get; set; }
    public ConfiguracaoRelatorioMensalBaseCalculo BaseCalculo { get; set; }
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
