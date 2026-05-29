using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RentalHub.Application.Auth;
using RentalHub.Application.Security;
using RentalHub.Domain.Entities;

namespace RentalHub.Infrastructure.Security;

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(Usuario usuario, Tenant tenant, IReadOnlyCollection<PermissaoDto> permissoes)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? "RentalHub";
        var audience = _configuration["Jwt:Audience"] ?? "RentalHub";
        var expiresMinutes = int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var value) ? value : 120;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Nome),
            new(ClaimTypes.Email, usuario.Email),
            new("TenantId", tenant.Id.ToString()),
            new("TenantSlug", tenant.Slug),
            new("TenantNome", tenant.Nome),
            new("TenantNomeExibicao", tenant.NomeExibicao),
            new("IsRootTenant", tenant.IsRootTenant.ToString().ToLowerInvariant()),
            new("TipoUsuario", ((int)usuario.TipoUsuario).ToString()),
            new("IsPlatformAdmin", usuario.IsPlatformAdmin.ToString().ToLowerInvariant())
        };

        foreach (var permissao in permissoes)
        {
            claims.Add(new Claim("permission", $"{permissao.Recurso}:{permissao.PodeVer}:{permissao.PodeEditar}:{permissao.PodeExcluir}"));
        }

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }
}

