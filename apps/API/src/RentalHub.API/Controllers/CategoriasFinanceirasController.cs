using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class CategoriasFinanceirasController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public CategoriasFinanceirasController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CategoriaFinanceiraResponse>>> GetAll(
        [FromQuery] MovimentacaoFinanceiraTipo? tipo,
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.CategoriasFinanceiras.AsNoTracking().AsQueryable();

        if (tipo.HasValue)
        {
            query = query.Where(c => c.Tipo == tipo.Value);
        }

        if (ativo.HasValue)
        {
            query = query.Where(c => c.Ativo == ativo.Value);
        }

        var items = await query
            .OrderBy(c => c.Tipo)
            .ThenBy(c => c.Nome)
            .Select(c => ToResponse(c))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoriaFinanceiraResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var categoria = await _dbContext.CategoriasFinanceiras
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return categoria is null ? NotFound() : Ok(ToResponse(categoria));
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaFinanceiraResponse>> Create(
        CategoriaFinanceiraRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return BadRequest(new { message = "Nome da categoria é obrigatório." });
        }

        var categoria = new CategoriaFinanceira
        {
            TenantId = _dbContext.CurrentTenantId,
            Nome = request.Nome.Trim(),
            Tipo = request.Tipo,
            Ativo = request.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.CategoriasFinanceiras.Add(categoria);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe uma categoria financeira com este nome e tipo." });
        }

        return CreatedAtAction(nameof(GetById), new { id = categoria.Id }, ToResponse(categoria));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoriaFinanceiraResponse>> Update(
        int id,
        CategoriaFinanceiraRequest request,
        CancellationToken cancellationToken)
    {
        var categoria = await _dbContext.CategoriasFinanceiras.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categoria is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return BadRequest(new { message = "Nome da categoria é obrigatório." });
        }

        categoria.Nome = request.Nome.Trim();
        categoria.Tipo = request.Tipo;
        categoria.Ativo = request.Ativo;
        categoria.DataAtualizacao = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe uma categoria financeira com este nome e tipo." });
        }

        return Ok(ToResponse(categoria));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var categoria = await _dbContext.CategoriasFinanceiras.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categoria is null)
        {
            return NotFound();
        }

        categoria.Ativo = false;
        categoria.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static CategoriaFinanceiraResponse ToResponse(CategoriaFinanceira categoria)
    {
        return new CategoriaFinanceiraResponse(
            categoria.Id,
            categoria.Nome,
            categoria.Tipo,
            categoria.Ativo,
            categoria.DataCriacao,
            categoria.DataAtualizacao);
    }
}

public sealed record CategoriaFinanceiraRequest(
    string Nome,
    MovimentacaoFinanceiraTipo Tipo,
    bool Ativo = true);

public sealed record CategoriaFinanceiraResponse(
    int Id,
    string Nome,
    MovimentacaoFinanceiraTipo Tipo,
    bool Ativo,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);
