using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Auth;
using RentalHub.Application.Security;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthController(
        RentalHubDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var usuario = await _dbContext.Usuarios
                .IgnoreQueryFilters()
                .Include(u => u.Tenant)
                .Include(u => u.PerfilAcesso)
                    .ThenInclude(p => p!.Permissoes)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.Ativo, cancellationToken);

            if (usuario?.Tenant is null || !_passwordHasher.Verify(request.Senha, usuario.SenhaHash))
            {
                return Unauthorized(new { message = "Email ou senha inválidos." });
            }

            var refreshToken = _tokenService.GenerateRefreshToken();
            usuario.RefreshTokenHash = _tokenService.HashRefreshToken(refreshToken);
            usuario.RefreshTokenExpiraEm = DateTime.UtcNow.AddDays(14);
            usuario.DataAtualizacao = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var permissoes = usuario.PerfilAcesso?.Permissoes
                .Select(p => new PermissaoDto(p.Recurso, p.PodeVer, p.PodeEditar, p.PodeExcluir))
                .ToArray() ?? [];

            return Ok(CreateResponse(usuario, usuario.Tenant, permissoes, refreshToken));
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Banco de dados indisponível. Verifique a connection string e a porta do PostgreSQL."
            });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioAuthDto>> Me(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var usuario = await _dbContext.Usuarios
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Include(u => u.PerfilAcesso)
                .ThenInclude(p => p!.Permissoes)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Ativo, cancellationToken);

        if (usuario?.Tenant is null)
        {
            return Unauthorized();
        }

        var permissoes = usuario.PerfilAcesso?.Permissoes
            .Select(p => new PermissaoDto(p.Recurso, p.PodeVer, p.PodeEditar, p.PodeExcluir))
            .ToArray() ?? [];

        return Ok(CreateUsuarioDto(usuario, usuario.Tenant, permissoes));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var refreshTokenHash = _tokenService.HashRefreshToken(request.RefreshToken);
        var usuario = await _dbContext.Usuarios
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .Include(u => u.PerfilAcesso)
                .ThenInclude(p => p!.Permissoes)
            .FirstOrDefaultAsync(
                u => u.RefreshTokenHash == refreshTokenHash &&
                     u.RefreshTokenExpiraEm > DateTime.UtcNow &&
                     u.Ativo,
                cancellationToken);

        if (usuario?.Tenant is null)
        {
            return Unauthorized();
        }

        var nextRefreshToken = _tokenService.GenerateRefreshToken();
        usuario.RefreshTokenHash = _tokenService.HashRefreshToken(nextRefreshToken);
        usuario.RefreshTokenExpiraEm = DateTime.UtcNow.AddDays(14);
        usuario.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var permissoes = usuario.PerfilAcesso?.Permissoes
            .Select(p => new PermissaoDto(p.Recurso, p.PodeVer, p.PodeEditar, p.PodeExcluir))
            .ToArray() ?? [];

        return Ok(CreateResponse(usuario, usuario.Tenant, permissoes, nextRefreshToken));
    }

    private AuthResponse CreateResponse(
        Domain.Entities.Usuario usuario,
        Domain.Entities.Tenant tenant,
        IReadOnlyCollection<PermissaoDto> permissoes,
        string refreshToken)
    {
        return new AuthResponse(
            _tokenService.GenerateAccessToken(usuario, tenant, permissoes),
            refreshToken,
            CreateUsuarioDto(usuario, tenant, permissoes));
    }

    private static UsuarioAuthDto CreateUsuarioDto(
        Domain.Entities.Usuario usuario,
        Domain.Entities.Tenant tenant,
        IReadOnlyCollection<PermissaoDto> permissoes)
    {
        return new UsuarioAuthDto(
            usuario.Id,
            tenant.Id,
            tenant.Slug,
            tenant.Nome,
            tenant.NomeExibicao,
            tenant.IsRootTenant,
            usuario.Nome,
            usuario.Email,
            (int)usuario.TipoUsuario,
            usuario.ProprietarioId,
            usuario.IsPlatformAdmin,
            permissoes);
    }
}
