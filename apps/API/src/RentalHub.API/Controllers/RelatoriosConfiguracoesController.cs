using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/relatorios/configuracoes-mensais")]
public sealed class RelatoriosConfiguracoesController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public RelatoriosConfiguracoesController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ConfiguracaoRelatorioMensalResponse>>> GetAll(
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ConfiguracoesRelatorioMensal.AsNoTracking().AsQueryable();

        if (ativo.HasValue)
        {
            query = query.Where(item => item.Ativo == ativo.Value);
        }

        var items = await query
            .OrderBy(item => item.Ordem)
            .ThenBy(item => item.Nome)
            .Select(item => ToResponse(item))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ConfiguracaoRelatorioMensalResponse>> Create(
        ConfiguracaoRelatorioMensalRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var entity = new ConfiguracaoRelatorioMensal
        {
            TenantId = _dbContext.CurrentTenantId,
            Nome = request.Nome.Trim(),
            TipoValor = request.TipoValor,
            Valor = request.Valor,
            BaseCalculo = request.BaseCalculo,
            Ordem = request.Ordem,
            Ativo = request.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.ConfiguracoesRelatorioMensal.Add(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe uma linha configurada com este nome." });
        }

        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, ToResponse(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ConfiguracaoRelatorioMensalResponse>> Update(
        int id,
        ConfiguracaoRelatorioMensalRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ConfiguracoesRelatorioMensal.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return validationError;
        }

        entity.Nome = request.Nome.Trim();
        entity.TipoValor = request.TipoValor;
        entity.Valor = request.Valor;
        entity.BaseCalculo = request.BaseCalculo;
        entity.Ordem = request.Ordem;
        entity.Ativo = request.Ativo;
        entity.DataAtualizacao = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe uma linha configurada com este nome." });
        }

        return Ok(ToResponse(entity));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ConfiguracoesRelatorioMensal.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Ativo = false;
        entity.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ActionResult? Validate(ConfiguracaoRelatorioMensalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return new BadRequestObjectResult(new { message = "Nome da linha é obrigatório." });
        }

        if (request.Valor < 0)
        {
            return new BadRequestObjectResult(new { message = "O valor da linha não pode ser negativo." });
        }

        if (request.TipoValor == ConfiguracaoRelatorioMensalTipoValor.Percentual && request.Valor > 100)
        {
            return new BadRequestObjectResult(new { message = "Percentual deve ficar entre 0% e 100%." });
        }

        if (request.Ordem < 0)
        {
            return new BadRequestObjectResult(new { message = "A ordem da linha não pode ser negativa." });
        }

        return null;
    }

    private static ConfiguracaoRelatorioMensalResponse ToResponse(ConfiguracaoRelatorioMensal entity)
    {
        return new ConfiguracaoRelatorioMensalResponse(
            entity.Id,
            entity.Nome,
            entity.TipoValor,
            entity.Valor,
            entity.BaseCalculo,
            entity.Ordem,
            entity.Ativo,
            entity.DataCriacao,
            entity.DataAtualizacao);
    }
}

public sealed record ConfiguracaoRelatorioMensalRequest(
    string Nome,
    ConfiguracaoRelatorioMensalTipoValor TipoValor,
    decimal Valor,
    ConfiguracaoRelatorioMensalBaseCalculo BaseCalculo,
    int Ordem,
    bool Ativo = true);

public sealed record ConfiguracaoRelatorioMensalResponse(
    int Id,
    string Nome,
    ConfiguracaoRelatorioMensalTipoValor TipoValor,
    decimal Valor,
    ConfiguracaoRelatorioMensalBaseCalculo BaseCalculo,
    int Ordem,
    bool Ativo,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);
