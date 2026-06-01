using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Security;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/perfis-acesso")]
public sealed class PerfisAcessoController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public PerfisAcessoController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PerfilAcessoResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var perfis = await _dbContext.PerfisAcesso
            .AsNoTracking()
            .Include(p => p.Permissoes)
            .OrderBy(p => p.Nome)
            .Select(p => new PerfilAcessoResponse(
                p.Id,
                p.Nome,
                p.Descricao,
                p.Ativo,
                p.Permissoes
                    .OrderBy(permissao => permissao.Recurso)
                    .Select(permissao => new PerfilAcessoPermissaoResponse(
                        permissao.Recurso,
                        permissao.PodeVer,
                        permissao.PodeEditar,
                        permissao.PodeExcluir))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Ok(perfis);
    }

    [HttpPost]
    public async Task<ActionResult<PerfilAcessoResponse>> Create(
        PerfilAcessoRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var exists = await _dbContext.PerfisAcesso
            .AnyAsync(p => p.Nome.ToLower() == request.Nome.Trim().ToLower(), cancellationToken);

        if (exists)
        {
            return BadRequest(new { message = "Já existe um perfil com este nome." });
        }

        var perfil = new PerfilAcesso
        {
            TenantId = _dbContext.CurrentTenantId,
            Nome = request.Nome.Trim(),
            Descricao = request.Descricao?.Trim() ?? string.Empty,
            Ativo = request.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        ApplyPermissions(perfil, request.Permissoes);
        _dbContext.PerfisAcesso.Add(perfil);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = perfil.Id }, ToResponse(perfil));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PerfilAcessoResponse>> Update(
        int id,
        PerfilAcessoRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var perfil = await _dbContext.PerfisAcesso
            .Include(p => p.Permissoes)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (perfil is null)
        {
            return NotFound();
        }

        var name = request.Nome.Trim();
        var duplicate = await _dbContext.PerfisAcesso
            .AnyAsync(p => p.Id != id && p.Nome.ToLower() == name.ToLower(), cancellationToken);

        if (duplicate)
        {
            return BadRequest(new { message = "Já existe um perfil com este nome." });
        }

        perfil.Nome = name;
        perfil.Descricao = request.Descricao?.Trim() ?? string.Empty;
        perfil.Ativo = request.Ativo;
        ApplyPermissions(perfil, request.Permissoes);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(perfil));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var perfil = await _dbContext.PerfisAcesso
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (perfil is null)
        {
            return NotFound();
        }

        perfil.Ativo = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static ActionResult? ValidateRequest(PerfilAcessoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return new BadRequestObjectResult(new { message = "Nome do perfil é obrigatório." });
        }

        var invalidResource = (request.Permissoes ?? [])
            .Select(p => NormalizeResource(p.Recurso))
            .FirstOrDefault(resource => !Resources.All.Contains(resource, StringComparer.OrdinalIgnoreCase));

        return invalidResource is null
            ? null
            : new BadRequestObjectResult(new { message = $"Recurso inválido: {invalidResource}." });
    }

    private void ApplyPermissions(PerfilAcesso perfil, IReadOnlyCollection<PerfilAcessoPermissaoRequest>? requestedPermissions)
    {
        var normalizedPermissions = (requestedPermissions ?? [])
            .GroupBy(permission => NormalizeResource(permission.Recurso), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);

        var removedPermissions = perfil.Permissoes
            .Where(permission => !normalizedPermissions.ContainsKey(permission.Recurso))
            .ToList();

        foreach (var permission in removedPermissions)
        {
            perfil.Permissoes.Remove(permission);
        }

        foreach (var (resource, request) in normalizedPermissions)
        {
            var permission = perfil.Permissoes.FirstOrDefault(item =>
                string.Equals(item.Recurso, resource, StringComparison.OrdinalIgnoreCase));

            if (permission is null)
            {
                permission = new PerfilAcessoPermissao
                {
                    TenantId = _dbContext.CurrentTenantId,
                    Recurso = resource
                };
                perfil.Permissoes.Add(permission);
            }

            permission.PodeVer = request.PodeVer || request.PodeEditar || request.PodeExcluir;
            permission.PodeEditar = request.PodeEditar;
            permission.PodeExcluir = request.PodeExcluir;
        }
    }

    private static PerfilAcessoResponse ToResponse(PerfilAcesso perfil)
    {
        return new PerfilAcessoResponse(
            perfil.Id,
            perfil.Nome,
            perfil.Descricao,
            perfil.Ativo,
            perfil.Permissoes
                .OrderBy(permission => permission.Recurso)
                .Select(permission => new PerfilAcessoPermissaoResponse(
                    permission.Recurso,
                    permission.PodeVer,
                    permission.PodeEditar,
                    permission.PodeExcluir))
                .ToList());
    }

    private static string NormalizeResource(string resource)
    {
        return resource.Trim().ToLowerInvariant();
    }
}

public sealed record PerfilAcessoRequest(
    string Nome,
    string? Descricao,
    bool Ativo,
    IReadOnlyCollection<PerfilAcessoPermissaoRequest>? Permissoes);

public sealed record PerfilAcessoPermissaoRequest(
    string Recurso,
    bool PodeVer,
    bool PodeEditar,
    bool PodeExcluir);

public sealed record PerfilAcessoResponse(
    int Id,
    string Nome,
    string Descricao,
    bool Ativo,
    IReadOnlyCollection<PerfilAcessoPermissaoResponse> Permissoes);

public sealed record PerfilAcessoPermissaoResponse(
    string Recurso,
    bool PodeVer,
    bool PodeEditar,
    bool PodeExcluir);
