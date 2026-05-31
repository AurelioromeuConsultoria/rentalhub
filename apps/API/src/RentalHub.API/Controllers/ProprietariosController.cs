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
public sealed class ProprietariosController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public ProprietariosController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProprietarioResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Proprietarios
            .AsNoTracking()
            .Include(p => p.Imoveis)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(p =>
                p.Nome.ToLower().Contains(normalizedSearch) ||
                p.Documento.ToLower().Contains(normalizedSearch) ||
                (p.Email != null && p.Email.ToLower().Contains(normalizedSearch)));
        }

        if (ativo.HasValue)
        {
            query = query.Where(p => p.Ativo == ativo.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProprietarioResponse(
                p.Id,
                p.Nome,
                p.Documento,
                p.Telefone,
                p.Email,
                p.DadosBancarios,
                p.Observacoes,
                p.Ativo,
                p.Imoveis.Count,
                p.DataCriacao,
                p.DataAtualizacao))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<ProprietarioResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProprietarioResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var proprietario = await _dbContext.Proprietarios
            .AsNoTracking()
            .Include(p => p.Imoveis)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return proprietario is null ? NotFound() : Ok(ToResponse(proprietario));
    }

    [HttpPost]
    public async Task<ActionResult<ProprietarioResponse>> Create(
        ProprietarioRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.Documento))
        {
            return BadRequest(new { message = "Nome e documento são obrigatórios." });
        }

        var proprietario = new Proprietario
        {
            TenantId = _dbContext.CurrentTenantId,
            Nome = request.Nome.Trim(),
            Documento = request.Documento.Trim(),
            Telefone = request.Telefone?.Trim(),
            Email = request.Email?.Trim(),
            DadosBancarios = request.DadosBancarios?.Trim(),
            Observacoes = request.Observacoes?.Trim(),
            Ativo = request.Ativo,
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.Proprietarios.Add(proprietario);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe um proprietário com este documento." });
        }

        return CreatedAtAction(nameof(GetById), new { id = proprietario.Id }, ToResponse(proprietario));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProprietarioResponse>> Update(
        int id,
        ProprietarioRequest request,
        CancellationToken cancellationToken)
    {
        var proprietario = await _dbContext.Proprietarios
            .Include(p => p.Imoveis)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (proprietario is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.Documento))
        {
            return BadRequest(new { message = "Nome e documento são obrigatórios." });
        }

        proprietario.Nome = request.Nome.Trim();
        proprietario.Documento = request.Documento.Trim();
        proprietario.Telefone = request.Telefone?.Trim();
        proprietario.Email = request.Email?.Trim();
        proprietario.DadosBancarios = request.DadosBancarios?.Trim();
        proprietario.Observacoes = request.Observacoes?.Trim();
        proprietario.Ativo = request.Ativo;
        proprietario.DataAtualizacao = DateTime.UtcNow;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe um proprietário com este documento." });
        }

        return Ok(ToResponse(proprietario));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var proprietario = await _dbContext.Proprietarios.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (proprietario is null)
        {
            return NotFound();
        }

        proprietario.Ativo = false;
        proprietario.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static ProprietarioResponse ToResponse(Proprietario proprietario)
    {
        return new ProprietarioResponse(
            proprietario.Id,
            proprietario.Nome,
            proprietario.Documento,
            proprietario.Telefone,
            proprietario.Email,
            proprietario.DadosBancarios,
            proprietario.Observacoes,
            proprietario.Ativo,
            proprietario.Imoveis.Count,
            proprietario.DataCriacao,
            proprietario.DataAtualizacao);
    }
}

public sealed record ProprietarioRequest(
    string Nome,
    string Documento,
    string? Telefone,
    string? Email,
    string? DadosBancarios,
    string? Observacoes,
    bool Ativo = true);

public sealed record ProprietarioResponse(
    int Id,
    string Nome,
    string Documento,
    string? Telefone,
    string? Email,
    string? DadosBancarios,
    string? Observacoes,
    bool Ativo,
    int TotalImoveis,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);
