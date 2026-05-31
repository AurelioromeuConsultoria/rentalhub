using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Common;
using RentalHub.Domain.Entities;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class HospedesController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public HospedesController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<HospedeResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Hospedes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(h =>
                h.Nome.ToLower().Contains(normalizedSearch) ||
                (h.Email != null && h.Email.ToLower().Contains(normalizedSearch)) ||
                (h.Documento != null && h.Documento.ToLower().Contains(normalizedSearch)));
        }

        if (ativo.HasValue)
        {
            query = query.Where(h => h.Ativo == ativo.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(h => h.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new HospedeResponse(
                h.Id,
                h.Nome,
                h.Email,
                h.Telefone,
                h.Documento,
                h.Nacionalidade,
                h.Observacoes,
                h.Ativo,
                h.DataCriacao,
                h.DataAtualizacao))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<HospedeResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HospedeResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var hospede = await _dbContext.Hospedes.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        return hospede is null ? NotFound() : Ok(ToResponse(hospede));
    }

    [HttpPost]
    public async Task<ActionResult<HospedeResponse>> Create(HospedeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return BadRequest(new { message = "Nome é obrigatório." });
        }

        var hospede = new Hospede
        {
            TenantId = _dbContext.CurrentTenantId,
            Nome = request.Nome.Trim(),
            Email = request.Email?.Trim(),
            Telefone = request.Telefone?.Trim(),
            Documento = request.Documento?.Trim(),
            Nacionalidade = request.Nacionalidade?.Trim(),
            Observacoes = request.Observacoes?.Trim(),
            Ativo = request.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.Hospedes.Add(hospede);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = hospede.Id }, ToResponse(hospede));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<HospedeResponse>> Update(
        int id,
        HospedeRequest request,
        CancellationToken cancellationToken)
    {
        var hospede = await _dbContext.Hospedes.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (hospede is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return BadRequest(new { message = "Nome é obrigatório." });
        }

        hospede.Nome = request.Nome.Trim();
        hospede.Email = request.Email?.Trim();
        hospede.Telefone = request.Telefone?.Trim();
        hospede.Documento = request.Documento?.Trim();
        hospede.Nacionalidade = request.Nacionalidade?.Trim();
        hospede.Observacoes = request.Observacoes?.Trim();
        hospede.Ativo = request.Ativo;
        hospede.DataAtualizacao = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(hospede));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var hospede = await _dbContext.Hospedes.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (hospede is null)
        {
            return NotFound();
        }

        hospede.Ativo = false;
        hospede.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static HospedeResponse ToResponse(Hospede hospede)
    {
        return new HospedeResponse(
            hospede.Id,
            hospede.Nome,
            hospede.Email,
            hospede.Telefone,
            hospede.Documento,
            hospede.Nacionalidade,
            hospede.Observacoes,
            hospede.Ativo,
            hospede.DataCriacao,
            hospede.DataAtualizacao);
    }
}

public sealed record HospedeRequest(
    string Nome,
    string? Email,
    string? Telefone,
    string? Documento,
    string? Nacionalidade,
    string? Observacoes,
    bool Ativo = true);

public sealed record HospedeResponse(
    int Id,
    string Nome,
    string? Email,
    string? Telefone,
    string? Documento,
    string? Nacionalidade,
    string? Observacoes,
    bool Ativo,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);
