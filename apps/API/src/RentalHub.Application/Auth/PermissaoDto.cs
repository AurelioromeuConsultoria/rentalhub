namespace RentalHub.Application.Auth;

public sealed record PermissaoDto(
    string Recurso,
    bool PodeVer,
    bool PodeEditar,
    bool PodeExcluir);

