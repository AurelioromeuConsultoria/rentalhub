namespace RentalHub.Application.Auth;

public sealed record UsuarioAuthDto(
    int Id,
    int TenantId,
    string TenantSlug,
    string TenantNome,
    string TenantNomeExibicao,
    bool IsRootTenant,
    string Nome,
    string Email,
    int TipoUsuario,
    int? ProprietarioId,
    bool IsPlatformAdmin,
    IReadOnlyCollection<PermissaoDto> Permissoes);
