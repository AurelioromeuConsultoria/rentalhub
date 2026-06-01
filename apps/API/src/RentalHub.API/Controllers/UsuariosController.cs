using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Common;
using RentalHub.Application.Security;
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

    public UsuariosController(RentalHubDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
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
            usuarios.Select(ToResponse).ToList(),
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

        return usuario is null ? NotFound() : Ok(ToResponse(usuario));
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioResponse>> Create(
        UsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateRequest(request, requirePassword: true, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var usuario = new Usuario
        {
            TenantId = _dbContext.CurrentTenantId,
            PerfilAcessoId = request.PerfilAcessoId,
            ProprietarioId = request.TipoUsuario == TipoUsuario.Proprietario ? request.ProprietarioId : null,
            Nome = request.Nome.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            SenhaHash = _passwordHasher.HashPassword(request.Senha!.Trim()),
            TipoUsuario = request.TipoUsuario,
            IsPlatformAdmin = request.IsPlatformAdmin,
            Ativo = request.Ativo,
            DataCriacao = DateTime.UtcNow
        };

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
        return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, ToResponse(usuario));
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

        if (!string.IsNullOrWhiteSpace(request.Senha))
        {
            usuario.SenhaHash = _passwordHasher.HashPassword(request.Senha.Trim());
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

        if (requirePassword && string.IsNullOrWhiteSpace(request.Senha))
        {
            return BadRequest(new { message = "Senha é obrigatória para novo usuário." });
        }

        if (!string.IsNullOrWhiteSpace(request.Senha) && request.Senha.Trim().Length < 8)
        {
            return BadRequest(new { message = "A senha deve ter pelo menos 8 caracteres." });
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
                return BadRequest(new { message = "Usuário proprietário deve estar vinculado a um proprietário." });
            }

            var proprietarioExists = await _dbContext.Proprietarios
                .AnyAsync(p => p.Id == request.ProprietarioId.Value && p.Ativo, cancellationToken);

            if (!proprietarioExists)
            {
                return BadRequest(new { message = "Proprietário inválido." });
            }
        }

        return null;
    }

    private async Task LoadNavigation(Usuario usuario, CancellationToken cancellationToken)
    {
        await _dbContext.Entry(usuario).Reference(u => u.PerfilAcesso).LoadAsync(cancellationToken);
        await _dbContext.Entry(usuario).Reference(u => u.Proprietario).LoadAsync(cancellationToken);
    }

    private static UsuarioResponse ToResponse(Usuario usuario)
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
            usuario.DataAtualizacao);
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
    bool Ativo = true);

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
    DateTime? DataAtualizacao);
