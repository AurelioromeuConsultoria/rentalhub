using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class Usuario : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int? PerfilAcessoId { get; set; }
    public PerfilAcesso? PerfilAcesso { get; set; }
    public int? ProprietarioId { get; set; }
    public Proprietario? Proprietario { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public TipoUsuario TipoUsuario { get; set; } = TipoUsuario.Operacional;
    public bool IsPlatformAdmin { get; set; }
    public bool Ativo { get; set; } = true;
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiraEm { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
