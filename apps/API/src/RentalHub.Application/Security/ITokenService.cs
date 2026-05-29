using RentalHub.Application.Auth;
using RentalHub.Domain.Entities;

namespace RentalHub.Application.Security;

public interface ITokenService
{
    string GenerateAccessToken(Usuario usuario, Tenant tenant, IReadOnlyCollection<PermissaoDto> permissoes);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

