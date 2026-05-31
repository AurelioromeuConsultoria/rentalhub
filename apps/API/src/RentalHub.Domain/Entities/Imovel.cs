using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class Imovel : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ProprietarioId { get; set; }
    public Proprietario? Proprietario { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string CodigoInterno { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? Endereco { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? Cep { get; set; }
    public int QuantidadeHospedes { get; set; }
    public int QuantidadeQuartos { get; set; }
    public int QuantidadeBanheiros { get; set; }
    public ImovelStatus Status { get; set; } = ImovelStatus.Ativo;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
    public List<ImovelComodidade> Comodidades { get; set; } = [];
    public List<ImovelFoto> Fotos { get; set; } = [];
}
