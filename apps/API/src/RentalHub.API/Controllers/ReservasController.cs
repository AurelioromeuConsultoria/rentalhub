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
public sealed class ReservasController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public ReservasController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ReservaResponse>>> GetAll(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] ReservaOrigem? origem,
        [FromQuery] ReservaStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .AsQueryable();

        if (inicio.HasValue)
        {
            var start = NormalizeDate(inicio.Value);
            query = query.Where(r => r.CheckOut > start);
        }

        if (fim.HasValue)
        {
            var end = NormalizeDate(fim.Value);
            query = query.Where(r => r.CheckIn < end);
        }

        if (imovelId.HasValue)
        {
            query = query.Where(r => r.ImovelId == imovelId.Value);
        }

        if (origem.HasValue)
        {
            query = query.Where(r => r.Origem == origem.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CheckIn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReservaResponse(
                r.Id,
                r.ImovelId,
                r.Imovel == null ? string.Empty : r.Imovel.Nome,
                r.HospedeId,
                r.Hospede == null ? string.Empty : r.Hospede.Nome,
                r.Origem,
                r.CheckIn,
                r.CheckOut,
                r.NumeroHospedes,
                r.ValorHospedagem,
                r.TaxaLimpeza,
                r.TaxaPlataforma,
                r.ComissaoAdministradora,
                r.ValorLiquido,
                r.Status,
                r.Observacoes,
                r.DataCriacao,
                r.DataAtualizacao))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<ReservaResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservaResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var reserva = await _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return reserva is null ? NotFound() : Ok(ToResponse(reserva));
    }

    [HttpPost]
    public async Task<ActionResult<ReservaResponse>> Create(ReservaRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateRequestAsync(request, null, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var reserva = new Reserva
        {
            TenantId = _dbContext.CurrentTenantId,
            ImovelId = request.ImovelId,
            HospedeId = request.HospedeId,
            Origem = request.Origem,
            CheckIn = NormalizeDate(request.CheckIn),
            CheckOut = NormalizeDate(request.CheckOut),
            NumeroHospedes = request.NumeroHospedes,
            ValorHospedagem = request.ValorHospedagem,
            TaxaLimpeza = request.TaxaLimpeza,
            TaxaPlataforma = request.TaxaPlataforma,
            ComissaoAdministradora = request.ComissaoAdministradora,
            ValorLiquido = CalculateValorLiquido(request),
            Status = request.Status,
            Observacoes = request.Observacoes?.Trim(),
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.Reservas.Add(reserva);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = reserva.Id }, ToResponse(reserva));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReservaResponse>> Update(
        int id,
        ReservaRequest request,
        CancellationToken cancellationToken)
    {
        var reserva = await _dbContext.Reservas
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (reserva is null)
        {
            return NotFound();
        }

        var validationError = await ValidateRequestAsync(request, id, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        reserva.ImovelId = request.ImovelId;
        reserva.HospedeId = request.HospedeId;
        reserva.Origem = request.Origem;
        reserva.CheckIn = NormalizeDate(request.CheckIn);
        reserva.CheckOut = NormalizeDate(request.CheckOut);
        reserva.NumeroHospedes = request.NumeroHospedes;
        reserva.ValorHospedagem = request.ValorHospedagem;
        reserva.TaxaLimpeza = request.TaxaLimpeza;
        reserva.TaxaPlataforma = request.TaxaPlataforma;
        reserva.ComissaoAdministradora = request.ComissaoAdministradora;
        reserva.ValorLiquido = CalculateValorLiquido(request);
        reserva.Status = request.Status;
        reserva.Observacoes = request.Observacoes?.Trim();
        reserva.DataAtualizacao = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(reserva));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var reserva = await _dbContext.Reservas.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (reserva is null)
        {
            return NotFound();
        }

        reserva.Status = ReservaStatus.Cancelada;
        reserva.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("disponibilidade")]
    public async Task<ActionResult<object>> CheckAvailability(
        [FromQuery] int imovelId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut,
        [FromQuery] int? reservaId,
        CancellationToken cancellationToken)
    {
        var start = NormalizeDate(checkIn);
        var end = NormalizeDate(checkOut);
        if (end <= start)
        {
            return BadRequest(new { message = "Check-out deve ser posterior ao check-in." });
        }

        var hasConflict = await HasConflictAsync(imovelId, start, end, reservaId, cancellationToken);
        return Ok(new { disponivel = !hasConflict });
    }

    private async Task<ActionResult?> ValidateRequestAsync(
        ReservaRequest request,
        int? reservaId,
        CancellationToken cancellationToken)
    {
        var checkIn = NormalizeDate(request.CheckIn);
        var checkOut = NormalizeDate(request.CheckOut);

        if (checkOut <= checkIn)
        {
            return BadRequest(new { message = "Check-out deve ser posterior ao check-in." });
        }

        if (request.NumeroHospedes < 1)
        {
            return BadRequest(new { message = "Número de hóspedes deve ser maior que zero." });
        }

        if (request.ValorHospedagem < 0 || request.TaxaLimpeza < 0 || request.TaxaPlataforma < 0 || request.ComissaoAdministradora < 0)
        {
            return BadRequest(new { message = "Valores financeiros não podem ser negativos." });
        }

        var imovel = await _dbContext.Imoveis.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.ImovelId, cancellationToken);
        if (imovel is null || imovel.Status == ImovelStatus.Inativo)
        {
            return BadRequest(new { message = "Imóvel ativo não encontrado." });
        }

        if (request.NumeroHospedes > imovel.QuantidadeHospedes)
        {
            return BadRequest(new { message = "Número de hóspedes excede a capacidade do imóvel." });
        }

        var hospedeExists = await _dbContext.Hospedes.AnyAsync(h => h.Id == request.HospedeId && h.Ativo, cancellationToken);
        if (!hospedeExists)
        {
            return BadRequest(new { message = "Hóspede ativo não encontrado." });
        }

        if (request.Status != ReservaStatus.Cancelada &&
            await HasConflictAsync(request.ImovelId, checkIn, checkOut, reservaId, cancellationToken))
        {
            return Conflict(new { message = "Já existe uma reserva, bloqueio ou manutenção conflitante para este imóvel no período informado." });
        }

        return null;
    }

    private async Task<bool> HasConflictAsync(
        int imovelId,
        DateTime checkIn,
        DateTime checkOut,
        int? reservaId,
        CancellationToken cancellationToken)
    {
        var hasReservationConflict = await _dbContext.Reservas.AnyAsync(
            r => r.ImovelId == imovelId &&
                 r.Status != ReservaStatus.Cancelada &&
                 (!reservaId.HasValue || r.Id != reservaId.Value) &&
                 r.CheckOut > checkIn &&
                 r.CheckIn < checkOut,
            cancellationToken);

        if (hasReservationConflict)
        {
            return true;
        }

        return await _dbContext.BloqueiosCalendario.AnyAsync(
            b => b.ImovelId == imovelId &&
                 b.Fim > checkIn &&
                 b.Inicio < checkOut,
            cancellationToken);
    }

    private static decimal CalculateValorLiquido(ReservaRequest request)
    {
        return request.ValorHospedagem +
               request.TaxaLimpeza -
               request.TaxaPlataforma -
               request.ComissaoAdministradora;
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static ReservaResponse ToResponse(Reserva reserva)
    {
        return new ReservaResponse(
            reserva.Id,
            reserva.ImovelId,
            reserva.Imovel?.Nome ?? string.Empty,
            reserva.HospedeId,
            reserva.Hospede?.Nome ?? string.Empty,
            reserva.Origem,
            reserva.CheckIn,
            reserva.CheckOut,
            reserva.NumeroHospedes,
            reserva.ValorHospedagem,
            reserva.TaxaLimpeza,
            reserva.TaxaPlataforma,
            reserva.ComissaoAdministradora,
            reserva.ValorLiquido,
            reserva.Status,
            reserva.Observacoes,
            reserva.DataCriacao,
            reserva.DataAtualizacao);
    }
}

public sealed record ReservaRequest(
    int ImovelId,
    int HospedeId,
    ReservaOrigem Origem,
    DateTime CheckIn,
    DateTime CheckOut,
    int NumeroHospedes,
    decimal ValorHospedagem,
    decimal TaxaLimpeza,
    decimal TaxaPlataforma,
    decimal ComissaoAdministradora,
    ReservaStatus Status,
    string? Observacoes);

public sealed record ReservaResponse(
    int Id,
    int ImovelId,
    string ImovelNome,
    int HospedeId,
    string HospedeNome,
    ReservaOrigem Origem,
    DateTime CheckIn,
    DateTime CheckOut,
    int NumeroHospedes,
    decimal ValorHospedagem,
    decimal TaxaLimpeza,
    decimal TaxaPlataforma,
    decimal ComissaoAdministradora,
    decimal ValorLiquido,
    ReservaStatus Status,
    string? Observacoes,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);
