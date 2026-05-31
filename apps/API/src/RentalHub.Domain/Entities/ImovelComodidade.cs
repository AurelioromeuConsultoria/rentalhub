namespace RentalHub.Domain.Entities;

public sealed class ImovelComodidade : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ImovelId { get; set; }
    public Imovel? Imovel { get; set; }
    public string Nome { get; set; } = string.Empty;
}
