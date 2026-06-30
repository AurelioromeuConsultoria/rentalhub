using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Common;
using RentalHub.Application.Services;
using RentalHub.Application.Security;
using RentalHub.API.Services;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class UsuariosController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly PasswordPolicyService _passwordPolicy;
    private readonly SecurityAuditService _securityAudit;

    public UsuariosController(
        RentalHubDbContext dbContext,
        IPasswordHasher passwordHasher,
        ICurrentUserContext currentUserContext,
        ITokenService tokenService,
        IEmailSender emailSender,
        IConfiguration configuration,
        PasswordPolicyService passwordPolicy,
        SecurityAuditService securityAudit)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _currentUserContext = currentUserContext;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _configuration = configuration;
        _passwordPolicy = passwordPolicy;
        _securityAudit = securityAudit;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<UsuarioResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.PerfilAcesso)
            .Include(u => u.Proprietario)
            .AsQueryable();

        if (!_currentUserContext.IsPlatformAdmin)
        {
            query = query.Where(u => !u.IsPlatformAdmin);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(u =>
                u.Nome.ToLower().Contains(normalizedSearch) ||
                u.Email.ToLower().Contains(normalizedSearch) ||
                (u.Proprietario != null && u.Proprietario.Nome.ToLower().Contains(normalizedSearch)));
        }

        if (ativo.HasValue)
        {
            query = query.Where(u => u.Ativo == ativo.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var usuarios = await query
            .OrderBy(u => u.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<UsuarioResponse>(
            usuarios.Select(usuario => ToResponse(usuario)).ToList(),
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var usuario = await _dbContext.Usuarios
            .AsNoTracking()
            .Include(u => u.PerfilAcesso)
            .Include(u => u.Proprietario)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (usuario?.IsPlatformAdmin == true && !_currentUserContext.IsPlatformAdmin)
        {
            return Forbid();
        }

        return usuario is null ? NotFound() : Ok(ToResponse(usuario));
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioResponse>> Create(
        UsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateRequest(request, requirePassword: !request.EnviarConvite, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var password = string.IsNullOrWhiteSpace(request.Senha)
            ? _tokenService.GenerateRefreshToken()
            : request.Senha.Trim();

        var usuario = new Usuario
        {
            TenantId = _dbContext.CurrentTenantId,
            PerfilAcessoId = request.PerfilAcessoId,
            ProprietarioId = request.TipoUsuario == TipoUsuario.Proprietario ? request.ProprietarioId : null,
            Nome = request.Nome.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            SenhaHash = _passwordHasher.HashPassword(password),
            TipoUsuario = request.TipoUsuario,
            IsPlatformAdmin = request.IsPlatformAdmin,
            Ativo = request.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        string? conviteUrl = null;
        if (request.EnviarConvite)
        {
            conviteUrl = ApplyInviteToken(usuario);
        }

        _dbContext.Usuarios.Add(usuario);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe um usuário com este e-mail." });
        }

        await LoadNavigation(usuario, cancellationToken);
        if (conviteUrl is not null)
        {
            await SendInviteEmailAsync(usuario, conviteUrl, cancellationToken);
            await _securityAudit.RecordAsync(
                "ConviteGerado",
                usuario.TenantId,
                usuario.Id.ToString(),
                usuario.Nome,
                usuario.Email,
                cancellationToken);
        }

        return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, ToResponse(usuario, conviteUrl));
    }

    [HttpPost("{id:int}/convite")]
    public async Task<ActionResult<UsuarioAccessLinkResponse>> GenerateInvite(
        int id,
        CancellationToken cancellationToken)
    {
        var usuario = await _dbContext.Usuarios
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (usuario is null)
        {
            return NotFound();
        }

        if (usuario.IsPlatformAdmin && !_currentUserContext.IsPlatformAdmin)
        {
            return Forbid();
        }

        var conviteUrl = ApplyInviteToken(usuario);
        usuario.RefreshTokenHash = null;
        usuario.RefreshTokenExpiraEm = null;
        usuario.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await SendInviteEmailAsync(usuario, conviteUrl, cancellationToken);
        await _securityAudit.RecordAsync(
            "ConviteGerado",
            usuario.TenantId,
            usuario.Id.ToString(),
            usuario.Nome,
            usuario.Email,
            cancellationToken);

        return Ok(new UsuarioAccessLinkResponse(conviteUrl, usuario.ConviteExpiraEm!.Value));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UsuarioResponse>> Update(
        int id,
        UsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await _dbContext.Usuarios
            .Include(u => u.PerfilAcesso)
            .Include(u => u.Proprietario)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (usuario is null)
        {
            return NotFound();
        }

        if (usuario.IsPlatformAdmin && !_currentUserContext.IsPlatformAdmin)
        {
            return Forbid();
        }

        var validation = await ValidateRequest(request, requirePassword: false, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        usuario.PerfilAcessoId = request.PerfilAcessoId;
        usuario.ProprietarioId = request.TipoUsuario == TipoUsuario.Proprietario ? request.ProprietarioId : null;
        usuario.Nome = request.Nome.Trim();
        usuario.Email = request.Email.Trim().ToLowerInvariant();
        usuario.TipoUsuario = request.TipoUsuario;
        usuario.IsPlatformAdmin = request.IsPlatformAdmin;
        usuario.Ativo = request.Ativo;
        usuario.DataAtualizacao = DateTime.UtcNow;

        var newPassword = request.Senha?.Trim();
        var passwordChanged = !string.IsNullOrWhiteSpace(newPassword);
        if (passwordChanged)
        {
            usuario.SenhaHash = _passwordHasher.HashPassword(newPassword!);
            usuario.RefreshTokenHash = null;
            usuario.RefreshTokenExpiraEm = null;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe um usuário com este e-mail." });
        }

        await LoadNavigation(usuario, cancellationToken);
        if (passwordChanged)
        {
            await _securityAudit.RecordAsync(
                "SenhaAlterada",
                usuario.TenantId,
                usuario.Id.ToString(),
                usuario.Nome,
                usuario.Email,
                cancellationToken);
        }

        return Ok(ToResponse(usuario));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var usuario = await _dbContext.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (usuario is null)
        {
            return NotFound();
        }

        if (usuario.IsPlatformAdmin && !_currentUserContext.IsPlatformAdmin)
        {
            return Forbid();
        }

        usuario.Ativo = false;
        usuario.DataAtualizacao = DateTime.UtcNow;
        usuario.RefreshTokenHash = null;
        usuario.RefreshTokenExpiraEm = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult?> ValidateRequest(
        UsuarioRequest request,
        bool requirePassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Nome e e-mail são obrigatórios." });
        }

        if (request.IsPlatformAdmin && !_currentUserContext.IsPlatformAdmin)
        {
            return Forbid();
        }

        if (request.IsPlatformAdmin)
        {
            var isRootTenant = await _dbContext.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(
                    tenant => tenant.Id == _dbContext.CurrentTenantId && tenant.IsRootTenant,
                    cancellationToken);

            if (!isRootTenant)
            {
                return BadRequest(new { message = "Administrador de plataforma só pode pertencer à empresa raiz." });
            }
        }

        if (request.EnviarConvite && !string.IsNullOrWhiteSpace(request.Senha))
        {
            return BadRequest(new { message = "Use senha ou convite, não os dois ao mesmo tempo." });
        }

        if (requirePassword && string.IsNullOrWhiteSpace(request.Senha))
        {
            return BadRequest(new { message = "Senha é obrigatória para novo usuário." });
        }

        if (!string.IsNullOrWhiteSpace(request.Senha))
        {
            var passwordError = _passwordPolicy.Validate(request.Senha, request.Nome, request.Email);
            if (passwordError is not null)
            {
                return BadRequest(new { message = passwordError });
            }
        }

        if (request.PerfilAcessoId.HasValue)
        {
            var perfilExists = await _dbContext.PerfisAcesso
                .AnyAsync(p => p.Id == request.PerfilAcessoId.Value && p.Ativo, cancellationToken);

            if (!perfilExists)
            {
                return BadRequest(new { message = "Perfil de acesso inválido." });
            }
        }

        if (request.TipoUsuario == TipoUsuario.Proprietario)
        {
            if (!request.ProprietarioId.HasValue)
            {
                return BadRequest(new { message = "Usuário sócio deve estar vinculado a um sócio." });
            }

            var proprietarioExists = await _dbContext.Proprietarios
                .AnyAsync(p => p.Id == request.ProprietarioId.Value && p.Ativo, cancellationToken);

            if (!proprietarioExists)
            {
                return BadRequest(new { message = "Sócio inválido." });
            }
        }

        return null;
    }

    private async Task LoadNavigation(Usuario usuario, CancellationToken cancellationToken)
    {
        await _dbContext.Entry(usuario).Reference(u => u.PerfilAcesso).LoadAsync(cancellationToken);
        await _dbContext.Entry(usuario).Reference(u => u.Proprietario).LoadAsync(cancellationToken);
    }

    private UsuarioResponse ToResponse(Usuario usuario, string? conviteUrl = null)
    {
        return new UsuarioResponse(
            usuario.Id,
            usuario.Nome,
            usuario.Email,
            usuario.TipoUsuario,
            usuario.PerfilAcessoId,
            usuario.PerfilAcesso?.Nome,
            usuario.ProprietarioId,
            usuario.Proprietario?.Nome,
            usuario.IsPlatformAdmin,
            usuario.Ativo,
            usuario.DataCriacao,
            usuario.DataAtualizacao,
            conviteUrl);
    }

    private string ApplyInviteToken(Usuario usuario)
    {
        var token = _tokenService.GenerateRefreshToken();
        usuario.ConviteTokenHash = _tokenService.HashRefreshToken(token);
        usuario.ConviteExpiraEm = DateTime.UtcNow.AddDays(7);
        usuario.ResetSenhaTokenHash = null;
        usuario.ResetSenhaExpiraEm = null;

        return BuildPasswordUrl(token);
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

    private async Task SendInviteEmailAsync(Usuario usuario, string conviteUrl, CancellationToken cancellationToken)
    {
        var text = $"""
            Olá, {usuario.Nome}.

            Você recebeu um convite para acessar o RentalHub.

            Definir senha: {conviteUrl}

            Este link expira em 7 dias.
            """;

        var html = $"""
            <p>Olá, {usuario.Nome}.</p>
            <p>Você recebeu um convite para acessar o RentalHub.</p>
            <p><a href="{conviteUrl}">Definir senha</a></p>
            <p>Este link expira em 7 dias.</p>
            """;

        await _emailSender.SendAsync(usuario.Email, "Convite para acessar o RentalHub", html, text, cancellationToken);
    }
}

public sealed record UsuarioRequest(
    string Nome,
    string Email,
    string? Senha,
    TipoUsuario TipoUsuario,
    int? PerfilAcessoId,
    int? ProprietarioId,
    bool IsPlatformAdmin = false,
    bool Ativo = true,
    bool EnviarConvite = false);

public sealed record UsuarioResponse(
    int Id,
    string Nome,
    string Email,
    TipoUsuario TipoUsuario,
    int? PerfilAcessoId,
    string? Perfil,
    int? ProprietarioId,
    string? Proprietario,
    bool IsPlatformAdmin,
    bool Ativo,
    DateTime DataCriacao,
    DateTime? DataAtualizacao,
    string? ConviteUrl = null);

public sealed record UsuarioAccessLinkResponse(string Url, DateTime ExpiraEm);
