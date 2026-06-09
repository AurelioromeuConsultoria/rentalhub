using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Auth;
using RentalHub.Application.Security;
using RentalHub.Application.Services;
using RentalHub.API.Services;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly PasswordPolicyService _passwordPolicy;
    private readonly SecurityAuditService _securityAudit;

    public AuthController(
        RentalHubDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEmailSender emailSender,
        IConfiguration configuration,
        IHostEnvironment environment,
        PasswordPolicyService passwordPolicy,
        SecurityAuditService securityAudit)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _configuration = configuration;
        _environment = environment;
        _passwordPolicy = passwordPolicy;
        _securityAudit = securityAudit;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
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
                await _securityAudit.RecordAsync(
                    "LoginFalhou",
                    usuario?.TenantId,
                    usuario?.Id.ToString() ?? "auth",
                    usuario?.Nome,
                    email,
                    cancellationToken);
                return Unauthorized(new { message = "Email ou senha inválidos." });
            }

            var refreshToken = _tokenService.GenerateRefreshToken();
            usuario.RefreshTokenHash = _tokenService.HashRefreshToken(refreshToken);
            usuario.RefreshTokenExpiraEm = DateTime.UtcNow.AddDays(14);
            usuario.DataAtualizacao = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _securityAudit.RecordAsync(
                "LoginOk",
                usuario.TenantId,
                usuario.Id.ToString(),
                usuario.Nome,
                usuario.Email,
                cancellationToken);

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

    [HttpPost("esqueci-senha")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<PasswordFlowResponse>> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Ok(new PasswordFlowResponse(
                "Se o e-mail estiver cadastrado, enviaremos as instruções para redefinir a senha."));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var usuario = await _dbContext.Usuarios
            .IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.Ativo, cancellationToken);

        string? resetUrl = null;
        if (usuario?.Tenant is not null)
        {
            var resetToken = _tokenService.GenerateRefreshToken();
            usuario.ResetSenhaTokenHash = _tokenService.HashRefreshToken(resetToken);
            usuario.ResetSenhaExpiraEm = DateTime.UtcNow.AddHours(2);
            usuario.RefreshTokenHash = null;
            usuario.RefreshTokenExpiraEm = null;
            usuario.DataAtualizacao = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            resetUrl = BuildPasswordUrl(resetToken);
            await SendPasswordEmailAsync(
                usuario.Email,
                "Redefinir senha no RentalHub",
                "Recebemos uma solicitação para redefinir sua senha no RentalHub.",
                "Redefinir senha",
                resetUrl,
                cancellationToken);
            await _securityAudit.RecordAsync(
                "ResetSolicitado",
                usuario.TenantId,
                usuario.Id.ToString(),
                usuario.Nome,
                usuario.Email,
                cancellationToken);
        }

        var exposeLink = _environment.IsDevelopment() ||
            _configuration.GetValue<bool>("Auth:ExposePasswordLinks");

        return Ok(new PasswordFlowResponse(
            "Se o e-mail estiver cadastrado, enviaremos as instruções para redefinir a senha.",
            exposeLink ? resetUrl : null));
    }

    [HttpPost("definir-senha")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Senha))
        {
            return BadRequest(new { message = "Token e nova senha são obrigatórios." });
        }

        var tokenHash = _tokenService.HashRefreshToken(request.Token.Trim());
        var now = DateTime.UtcNow;
        var usuario = await _dbContext.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                u => (u.ConviteTokenHash == tokenHash && u.ConviteExpiraEm > now) ||
                     (u.ResetSenhaTokenHash == tokenHash && u.ResetSenhaExpiraEm > now),
                cancellationToken);

        if (usuario is null)
        {
            return BadRequest(new { message = "Link inválido ou expirado." });
        }

        var passwordError = _passwordPolicy.Validate(request.Senha, usuario.Nome, usuario.Email);
        if (passwordError is not null)
        {
            return BadRequest(new { message = passwordError });
        }

        usuario.SenhaHash = _passwordHasher.HashPassword(request.Senha.Trim());
        usuario.ConviteTokenHash = null;
        usuario.ConviteExpiraEm = null;
        usuario.ResetSenhaTokenHash = null;
        usuario.ResetSenhaExpiraEm = null;
        usuario.RefreshTokenHash = null;
        usuario.RefreshTokenExpiraEm = null;
        usuario.Ativo = true;
        usuario.DataAtualizacao = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _securityAudit.RecordAsync(
            "SenhaAlterada",
            usuario.TenantId,
            usuario.Id.ToString(),
            usuario.Nome,
            usuario.Email,
            cancellationToken);

        return Ok(new { message = "Senha definida com sucesso. Você já pode entrar no RentalHub." });
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

    private string BuildPasswordUrl(string token)
    {
        var configuredAdminUrl = _configuration["App:AdminUrl"] ?? _configuration["Frontend:BaseUrl"];
        var origin = Request.Headers.Origin.ToString();
        var baseUrl = !string.IsNullOrWhiteSpace(configuredAdminUrl)
            ? configuredAdminUrl
            : !string.IsNullOrWhiteSpace(origin)
                ? origin
                : $"{Request.Scheme}://{Request.Host}";

        return $"{baseUrl.TrimEnd('/')}/definir-senha?token={Uri.EscapeDataString(token)}";
    }

    private async Task SendPasswordEmailAsync(
        string email,
        string subject,
        string intro,
        string action,
        string url,
        CancellationToken cancellationToken)
    {
        var text = $"{intro}\n\n{action}: {url}\n\nSe você não solicitou isso, ignore esta mensagem.";
        var html = $"""
            <p>{intro}</p>
            <p><a href="{url}">{action}</a></p>
            <p>Se você não solicitou isso, ignore esta mensagem.</p>
            """;

        await _emailSender.SendAsync(email, subject, html, text, cancellationToken);
    }
}

public sealed record ForgotPasswordRequest(string Email);

public sealed record SetPasswordRequest(string Token, string Senha);

public sealed record PasswordFlowResponse(string Message, string? Url = null);
