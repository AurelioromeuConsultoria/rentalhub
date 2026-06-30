using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Common;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class LimpezasController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public LimpezasController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<LimpezaResponse>>> GetAll(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] int? reservaId,
        [FromQuery] LimpezaStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = BaseQuery();

        if (inicio.HasValue)
        {
            query = query.Where(l => l.DataPrevista >= NormalizeDate(inicio.Value));
        }

        if (fim.HasValue)
        {
            query = query.Where(l => l.DataPrevista <= NormalizeDate(fim.Value));
        }

        if (imovelId.HasValue)
        {
            query = query.Where(l => l.ImovelId == imovelId.Value);
        }

        if (reservaId.HasValue)
        {
            query = query.Where(l => l.ReservaId == reservaId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }
        else
        {
            query = query.Where(l => l.Status != LimpezaStatus.Cancelada);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(l => l.DataPrevista)
            .ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => ToResponse(l))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<LimpezaResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LimpezaResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var limpeza = await BaseQuery().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        return limpeza is null ? NotFound() : Ok(ToResponse(limpeza));
    }

    [HttpPost]
    public async Task<ActionResult<LimpezaResponse>> Create(LimpezaRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var limpeza = new Limpeza
        {
            TenantId = _dbContext.CurrentTenantId,
            ImovelId = request.ImovelId,
            ReservaId = request.ReservaId,
            DataPrevista = NormalizeDate(request.DataPrevista),
            Responsavel = request.Responsavel.Trim(),
            Valor = request.Valor,
            Status = request.Status,
            Observacoes = request.Observacoes?.Trim(),
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.Limpezas.Add(limpeza);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await BaseQuery().FirstAsync(l => l.Id == limpeza.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = limpeza.Id }, ToResponse(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LimpezaResponse>> Update(int id, LimpezaRequest request, CancellationToken cancellationToken)
    {
        var limpeza = await _dbContext.Limpezas.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (limpeza is null)
        {
            return NotFound();
        }

        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        limpeza.ImovelId = request.ImovelId;
        limpeza.ReservaId = request.ReservaId;
        limpeza.DataPrevista = NormalizeDate(request.DataPrevista);
        limpeza.Responsavel = request.Responsavel.Trim();
        limpeza.Valor = request.Valor;
        limpeza.Status = request.Status;
        limpeza.Observacoes = request.Observacoes?.Trim();
        limpeza.DataAtualizacao = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await BaseQuery().FirstAsync(l => l.Id == limpeza.Id, cancellationToken);
        return Ok(ToResponse(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var limpeza = await _dbContext.Limpezas.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (limpeza is null)
        {
            return NotFound();
        }

        limpeza.Status = LimpezaStatus.Cancelada;
        limpeza.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private IQueryable<Limpeza> BaseQuery()
    {
        return _dbContext.Limpezas
            .AsNoTracking()
            .Include(l => l.Imovel)
            .Include(l => l.Reserva);
    }

    private async Task<ActionResult?> ValidateRequestAsync(LimpezaRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Responsavel))
        {
            return BadRequest(new { message = "Responsável pela limpeza é obrigatório." });
        }

        if (request.Valor < 0)
        {
            return BadRequest(new { message = "Valor da limpeza não pode ser negativo." });
        }

        var imovelExists = await _dbContext.Imoveis.AnyAsync(
            i => i.Id == request.ImovelId && i.Status != ImovelStatus.Inativo,
            cancellationToken);

        if (!imovelExists)
        {
            return BadRequest(new { message = "Imóvel ativo não encontrado." });
        }

        if (request.ReservaId.HasValue)
        {
            var reserva = await _dbContext.Reservas
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == request.ReservaId.Value, cancellationToken);

            if (reserva is null)
            {
                return BadRequest(new { message = "Reserva não encontrada." });
            }

            if (reserva.ImovelId != request.ImovelId)
            {
                return BadRequest(new { message = "Reserva informada não pertence ao imóvel selecionado." });
            }
        }

        return null;
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static LimpezaResponse ToResponse(Limpeza limpeza)
    {
        return new LimpezaResponse(
            limpeza.Id,
            limpeza.ImovelId,
            limpeza.Imovel?.Nome ?? string.Empty,
            limpeza.ReservaId,
            limpeza.DataPrevista,
            limpeza.Responsavel,
            limpeza.Valor,
            limpeza.Status,
            limpeza.Observacoes,
            limpeza.DataCriacao,
            limpeza.DataAtualizacao);
    }
}

public sealed record LimpezaRequest(
    int ImovelId,
    int? ReservaId,
    DateTime DataPrevista,
    string Responsavel,
    decimal Valor,
    LimpezaStatus Status,
    string? Observacoes);

public sealed record LimpezaResponse(
    int Id,
    int ImovelId,
    string ImovelNome,
    int? ReservaId,
    DateTime DataPrevista,
    string Responsavel,
    decimal Valor,
    LimpezaStatus Status,
    string? Observacoes,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);
