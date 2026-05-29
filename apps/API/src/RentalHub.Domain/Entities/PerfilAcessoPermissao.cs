namespace RentalHub.Domain.Entities;

public sealed class PerfilAcessoPermissao : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int PerfilAcessoId { get; set; }
    public PerfilAcesso? PerfilAcesso { get; set; }
    public string Recurso { get; set; } = string.Empty;
    public bool PodeVer { get; set; }
    public bool PodeEditar { get; set; }
    public bool PodeExcluir { get; set; }
}

