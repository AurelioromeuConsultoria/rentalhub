namespace RentalHub.Domain.Entities;

public sealed class ImovelFoto : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ImovelId { get; set; }
    public Imovel? Imovel { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int Ordem { get; set; }
    public bool Principal { get; set; }
}
