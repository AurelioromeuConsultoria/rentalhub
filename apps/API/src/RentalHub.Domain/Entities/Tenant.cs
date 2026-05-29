namespace RentalHub.Domain.Entities;

public sealed class Tenant
{
    public const int InitialTenantId = 1;
    public const string InitialTenantName = "RentalHub";
    public const string InitialTenantSlug = "rentalhub";

    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string NomeExibicao { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsRootTenant { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public ICollection<TenantDomain> Domains { get; set; } = [];
}
