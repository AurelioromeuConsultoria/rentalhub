using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Security;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Domain.Security;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TenantsController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    public TenantsController(RentalHubDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TenantResponse>>> GetAll(CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var tenants = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Domains)
            .OrderBy(t => t.Nome)
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(t => t.Id).ToArray();
        var usuariosPorTenant = await _dbContext.Usuarios
            .IgnoreQueryFilters()
            .Where(u => tenantIds.Contains(u.TenantId))
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        var imoveisPorTenant = await _dbContext.Imoveis
            .IgnoreQueryFilters()
            .Where(i => tenantIds.Contains(i.TenantId))
            .GroupBy(i => i.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        var reservasPorTenant = await _dbContext.Reservas
            .IgnoreQueryFilters()
            .Where(r => tenantIds.Contains(r.TenantId))
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        return Ok(tenants
            .Select(t => ToResponse(
                t,
                usuariosPorTenant.GetValueOrDefault(t.Id),
                imoveisPorTenant.GetValueOrDefault(t.Id),
                reservasPorTenant.GetValueOrDefault(t.Id)))
            .ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TenantResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        var usuarios = await _dbContext.Usuarios.IgnoreQueryFilters().CountAsync(u => u.TenantId == id, cancellationToken);
        var imoveis = await _dbContext.Imoveis.IgnoreQueryFilters().CountAsync(i => i.TenantId == id, cancellationToken);
        var reservas = await _dbContext.Reservas.IgnoreQueryFilters().CountAsync(r => r.TenantId == id, cancellationToken);

        return Ok(ToResponse(tenant, usuarios, imoveis, reservas));
    }

    [HttpPost]
    public async Task<ActionResult<TenantResponse>> Create(TenantRequest request, CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var slug = NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Nome : request.Slug);
        var slugExists = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == slug, cancellationToken);

        if (slugExists)
        {
            return Conflict(new { message = "Já existe uma empresa com este slug." });
        }

        var domains = NormalizeDomains(request.Domains);
        if (domains.Count > 0)
        {
            var existingDomain = await _dbContext.TenantDomains
                .IgnoreQueryFilters()
                .AnyAsync(d => domains.Contains(d.Domain), cancellationToken);

            if (existingDomain)
            {
                return Conflict(new { message = "Já existe uma empresa usando um dos domínios informados." });
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tenant = new Tenant
        {
            Nome = request.Nome.Trim(),
            NomeExibicao = request.NomeExibicao.Trim(),
            Slug = slug,
            IsRootTenant = false,
            Ativo = request.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        ApplyDomains(tenant, domains);
        var perfil = CreateAdminProfile(tenant.Id);
        _dbContext.PerfisAcesso.Add(perfil);

        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            _dbContext.Usuarios.Add(new Usuario
            {
                TenantId = tenant.Id,
                PerfilAcesso = perfil,
                Nome = string.IsNullOrWhiteSpace(request.AdminNome) ? request.NomeExibicao.Trim() : request.AdminNome.Trim(),
                Email = request.AdminEmail.Trim().ToLowerInvariant(),
                SenhaHash = _passwordHasher.HashPassword(request.AdminSenha!.Trim()),
                TipoUsuario = TipoUsuario.Administrador,
                IsPlatformAdmin = false,
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Conflict(new { message = "Não foi possível criar a empresa. Verifique slug, domínios e e-mail do admin." });
        }

        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, ToResponse(tenant, string.IsNullOrWhiteSpace(request.AdminEmail) ? 0 : 1, 0, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TenantResponse>> Update(
        int id,
        TenantRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        var validation = ValidateRequest(request, validateAdmin: false);
        if (validation is not null)
        {
            return validation;
        }

        var slug = NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Nome : request.Slug);
        var slugExists = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id != id && t.Slug == slug, cancellationToken);

        if (slugExists)
        {
            return Conflict(new { message = "Já existe uma empresa com este slug." });
        }

        var domains = NormalizeDomains(request.Domains);
        if (domains.Count > 0)
        {
            var existingDomain = await _dbContext.TenantDomains
                .IgnoreQueryFilters()
                .AnyAsync(d => d.TenantId != id && domains.Contains(d.Domain), cancellationToken);

            if (existingDomain)
            {
                return Conflict(new { message = "Já existe outra empresa usando um dos domínios informados." });
            }
        }

        tenant.Nome = request.Nome.Trim();
        tenant.NomeExibicao = request.NomeExibicao.Trim();
        tenant.Slug = slug;
        tenant.Ativo = request.Ativo;
        ApplyDomains(tenant, domains);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var usuarios = await _dbContext.Usuarios.IgnoreQueryFilters().CountAsync(u => u.TenantId == id, cancellationToken);
        var imoveis = await _dbContext.Imoveis.IgnoreQueryFilters().CountAsync(i => i.TenantId == id, cancellationToken);
        var reservas = await _dbContext.Reservas.IgnoreQueryFilters().CountAsync(r => r.TenantId == id, cancellationToken);

        return Ok(ToResponse(tenant, usuarios, imoveis, reservas));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var tenant = await _dbContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        if (tenant.IsRootTenant)
        {
            return BadRequest(new { message = "A empresa raiz não pode ser inativada." });
        }

        tenant.Ativo = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private bool IsPlatformAdmin()
    {
        return string.Equals(
            User.FindFirst("IsPlatformAdmin")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private ActionResult? ValidateRequest(TenantRequest request, bool validateAdmin = true)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.NomeExibicao))
        {
            return BadRequest(new { message = "Nome e nome de exibição são obrigatórios." });
        }

        if (validateAdmin && !string.IsNullOrWhiteSpace(request.AdminEmail) &&
            (string.IsNullOrWhiteSpace(request.AdminSenha) || request.AdminSenha.Trim().Length < 8))
        {
            return BadRequest(new { message = "A senha do admin deve ter pelo menos 8 caracteres." });
        }

        return null;
    }

    private static string NormalizeSlug(string? value)
    {
        var slug = Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"empresa-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" : slug;
    }

    private static IReadOnlyCollection<string> NormalizeDomains(IReadOnlyCollection<string>? domains)
    {
        return (domains ?? [])
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .ToList();
    }

    private static PerfilAcesso CreateAdminProfile(int tenantId)
    {
        return new PerfilAcesso
        {
            TenantId = tenantId,
            Nome = "Administrador",
            Descricao = "Acesso total ao tenant.",
            Ativo = true,
            DataCriacao = DateTime.UtcNow,
            Permissoes = Resources.All.Select(resource => new PerfilAcessoPermissao
            {
                TenantId = tenantId,
                Recurso = resource,
                PodeVer = true,
                PodeEditar = true,
                PodeExcluir = true
            }).ToList()
        };
    }

    private void ApplyDomains(Tenant tenant, IReadOnlyCollection<string> domains)
    {
        foreach (var domain in tenant.Domains.Where(d => !domains.Contains(d.Domain)))
        {
            domain.Ativo = false;
            domain.IsPrimary = false;
        }

        var index = 0;
        foreach (var domain in domains)
        {
            var existing = tenant.Domains.FirstOrDefault(d => d.Domain == domain);
            if (existing is null)
            {
                tenant.Domains.Add(new TenantDomain
                {
                    TenantId = tenant.Id,
                    Domain = domain,
                    IsPrimary = index == 0,
                    Ativo = true
                });
            }
            else
            {
                existing.IsPrimary = index == 0;
                existing.Ativo = true;
            }

            index++;
        }
    }

    private static TenantResponse ToResponse(Tenant tenant, int usuarios, int imoveis, int reservas)
    {
        return new TenantResponse(
            tenant.Id,
            tenant.Nome,
            tenant.NomeExibicao,
            tenant.Slug,
            tenant.IsRootTenant,
            tenant.Ativo,
            tenant.Domains
                .Where(d => d.Ativo)
                .OrderByDescending(d => d.IsPrimary)
                .ThenBy(d => d.Domain)
                .Select(d => d.Domain)
                .ToList(),
            usuarios,
            imoveis,
            reservas,
            tenant.DataCriacao);
    }
}

public sealed record TenantRequest(
    string Nome,
    string NomeExibicao,
    string? Slug,
    IReadOnlyCollection<string>? Domains,
    bool Ativo = true,
    string? AdminNome = null,
    string? AdminEmail = null,
    string? AdminSenha = null);

public sealed record TenantResponse(
    int Id,
    string Nome,
    string NomeExibicao,
    string Slug,
    bool IsRootTenant,
    bool Ativo,
    IReadOnlyCollection<string> Domains,
    int Usuarios,
    int Imoveis,
    int Reservas,
    DateTime DataCriacao);
