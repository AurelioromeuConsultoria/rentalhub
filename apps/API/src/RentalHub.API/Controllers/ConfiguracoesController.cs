using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Security;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ConfiguracoesController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public ConfiguracoesController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracoesResponse>> Get(CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == _dbContext.CurrentTenantId, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        var resumo = new ConfiguracoesResumoResponse(
            await _dbContext.Usuarios.CountAsync(cancellationToken),
            await _dbContext.Proprietarios.CountAsync(cancellationToken),
            await _dbContext.Imoveis.CountAsync(cancellationToken),
            await _dbContext.Reservas.CountAsync(cancellationToken),
            await _dbContext.MovimentacoesFinanceiras.CountAsync(cancellationToken),
            await _dbContext.RepassesProprietarios.CountAsync(cancellationToken));

        return Ok(new ConfiguracoesResponse(
            ToTenantResponse(tenant),
            Resources.All,
            resumo));
    }

    [HttpPut("tenant")]
    public async Task<ActionResult<TenantConfiguracaoResponse>> UpdateTenant(
        TenantConfiguracaoRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == _dbContext.CurrentTenantId, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.NomeExibicao))
        {
            return BadRequest(new { message = "Nome e nome de exibição são obrigatórios." });
        }

        tenant.Nome = request.Nome.Trim();
        tenant.NomeExibicao = request.NomeExibicao.Trim();
        tenant.Ativo = request.Ativo;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToTenantResponse(tenant));
    }

    private static TenantConfiguracaoResponse ToTenantResponse(Tenant tenant)
    {
        return new TenantConfiguracaoResponse(
            tenant.Id,
            tenant.Nome,
            tenant.NomeExibicao,
            tenant.Slug,
            tenant.IsRootTenant,
            tenant.Ativo,
            tenant.Domains
                .OrderByDescending(d => d.IsPrimary)
                .ThenBy(d => d.Domain)
                .Select(d => d.Domain)
                .ToList(),
            tenant.DataCriacao);
    }
}

public sealed record TenantConfiguracaoRequest(
    string Nome,
    string NomeExibicao,
    bool Ativo = true);

public sealed record ConfiguracoesResumoResponse(
    int Usuarios,
    int Proprietarios,
    int Imoveis,
    int Reservas,
    int Movimentacoes,
    int Repasses);

public sealed record TenantConfiguracaoResponse(
    int Id,
    string Nome,
    string NomeExibicao,
    string Slug,
    bool IsRootTenant,
    bool Ativo,
    IReadOnlyCollection<string> Domains,
    DateTime DataCriacao);

public sealed record ConfiguracoesResponse(
    TenantConfiguracaoResponse Tenant,
    IReadOnlyCollection<string> Recursos,
    ConfiguracoesResumoResponse Resumo);
