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
public sealed class CalendarioController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public CalendarioController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CalendarioEventoResponse>>> GetEvents(
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        [FromQuery] int? imovelId,
        CancellationToken cancellationToken)
    {
        var start = NormalizeDate(inicio);
        var end = NormalizeDate(fim);
        if (end <= start)
        {
            return BadRequest(new { message = "Fim deve ser posterior ao início." });
        }

        var reservasQuery = _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .Where(r => r.Status != ReservaStatus.Cancelada && r.CheckOut > start && r.CheckIn < end);

        var bloqueiosQuery = _dbContext.BloqueiosCalendario
            .AsNoTracking()
            .Include(b => b.Imovel)
            .Where(b => b.Fim > start && b.Inicio < end);

        var limpezasQuery = _dbContext.Limpezas
            .AsNoTracking()
            .Include(l => l.Imovel)
            .Where(l => l.Status != LimpezaStatus.Cancelada && l.DataPrevista >= start && l.DataPrevista < end);

        var manutencoesQuery = _dbContext.Manutencoes
            .AsNoTracking()
            .Include(m => m.Imovel)
            .Where(m => m.Status != ManutencaoStatus.Cancelada &&
                ((m.DataPrevista.HasValue && m.DataPrevista.Value >= start && m.DataPrevista.Value < end) ||
                 (!m.DataPrevista.HasValue && m.DataAbertura >= start && m.DataAbertura < end)));

        if (imovelId.HasValue)
        {
            reservasQuery = reservasQuery.Where(r => r.ImovelId == imovelId.Value);
            bloqueiosQuery = bloqueiosQuery.Where(b => b.ImovelId == imovelId.Value);
            limpezasQuery = limpezasQuery.Where(l => l.ImovelId == imovelId.Value);
            manutencoesQuery = manutencoesQuery.Where(m => m.ImovelId == imovelId.Value);
        }

        var reservas = await reservasQuery
            .Select(r => new CalendarioEventoResponse(
                $"reserva-{r.Id}",
                r.Id,
                "reserva",
                r.Status == ReservaStatus.Pendente ? "Reserva pendente" : "Reserva",
                r.ImovelId,
                r.Imovel == null ? string.Empty : r.Imovel.Nome,
                r.CheckIn,
                r.CheckOut,
                r.Hospede == null ? string.Empty : r.Hospede.Nome,
                (int)r.Status))
            .ToListAsync(cancellationToken);

        var bloqueios = await bloqueiosQuery
            .Select(b => new CalendarioEventoResponse(
                $"bloqueio-{b.Id}",
                b.Id,
                b.Tipo == BloqueioCalendarioTipo.Manutencao ? "manutencao" : "bloqueio",
                b.Motivo,
                b.ImovelId,
                b.Imovel == null ? string.Empty : b.Imovel.Nome,
                b.Inicio,
                b.Fim,
                b.Motivo,
                (int)b.Tipo))
            .ToListAsync(cancellationToken);

        var limpezas = await limpezasQuery
            .Select(l => new CalendarioEventoResponse(
                $"limpeza-{l.Id}",
                l.Id,
                "limpeza",
                "Limpeza",
                l.ImovelId,
                l.Imovel == null ? string.Empty : l.Imovel.Nome,
                l.DataPrevista,
                l.DataPrevista.AddDays(1),
                l.Responsavel,
                (int)l.Status))
            .ToListAsync(cancellationToken);

        var manutencoes = await manutencoesQuery
            .Select(m => new CalendarioEventoResponse(
                $"ocorrencia-manutencao-{m.Id}",
                m.Id,
                "manutencao",
                m.Categoria,
                m.ImovelId,
                m.Imovel == null ? string.Empty : m.Imovel.Nome,
                m.DataPrevista ?? m.DataAbertura,
                (m.DataPrevista ?? m.DataAbertura).AddDays(1),
                m.Descricao,
                (int)m.Status))
            .ToListAsync(cancellationToken);

        return Ok(reservas
            .Concat(bloqueios)
            .Concat(limpezas)
            .Concat(manutencoes)
            .OrderBy(e => e.Inicio)
            .ThenBy(e => e.ImovelNome)
            .ToArray());
    }

    [HttpPost("bloqueios")]
    public async Task<ActionResult<CalendarioEventoResponse>> CreateBlock(
        BloqueioCalendarioRequest request,
        CancellationToken cancellationToken)
    {
        var start = NormalizeDate(request.Inicio);
        var end = NormalizeDate(request.Fim);

        if (end <= start)
        {
            return BadRequest(new { message = "Fim deve ser posterior ao início." });
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return BadRequest(new { message = "Motivo é obrigatório." });
        }

        var imovelExists = await _dbContext.Imoveis
            .AnyAsync(i => i.Id == request.ImovelId && i.Status != ImovelStatus.Inativo, cancellationToken);

        if (!imovelExists)
        {
            return BadRequest(new { message = "Imóvel ativo não encontrado." });
        }

        if (await HasReservationConflictAsync(request.ImovelId, start, end, cancellationToken))
        {
            return Conflict(new { message = "Existe reserva ativa nesse período para o imóvel." });
        }

        if (await HasBlockConflictAsync(request.ImovelId, start, end, cancellationToken))
        {
            return Conflict(new { message = "Existe bloqueio ou manutenção nesse período para o imóvel." });
        }

        var bloqueio = new BloqueioCalendario
        {
            TenantId = _dbContext.CurrentTenantId,
            ImovelId = request.ImovelId,
            Inicio = start,
            Fim = end,
            Tipo = request.Tipo,
            Motivo = request.Motivo.Trim(),
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.BloqueiosCalendario.Add(bloqueio);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var imovel = await _dbContext.Imoveis.AsNoTracking().FirstAsync(i => i.Id == bloqueio.ImovelId, cancellationToken);

        return CreatedAtAction(
            nameof(GetEvents),
            new { inicio = start, fim = end, imovelId = bloqueio.ImovelId },
            ToResponse(bloqueio, imovel.Nome));
    }

    [HttpDelete("bloqueios/{id:int}")]
    public async Task<IActionResult> DeleteBlock(int id, CancellationToken cancellationToken)
    {
        var bloqueio = await _dbContext.BloqueiosCalendario.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bloqueio is null)
        {
            return NotFound();
        }

        _dbContext.BloqueiosCalendario.Remove(bloqueio);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<bool> HasReservationConflictAsync(
        int imovelId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Reservas.AnyAsync(
            r => r.ImovelId == imovelId &&
                 r.Status != ReservaStatus.Cancelada &&
                 r.CheckOut > start &&
                 r.CheckIn < end,
            cancellationToken);
    }

    private async Task<bool> HasBlockConflictAsync(
        int imovelId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        return await _dbContext.BloqueiosCalendario.AnyAsync(
            b => b.ImovelId == imovelId &&
                 b.Fim > start &&
                 b.Inicio < end,
            cancellationToken);
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static CalendarioEventoResponse ToResponse(BloqueioCalendario bloqueio, string imovelNome)
    {
        return new CalendarioEventoResponse(
            $"bloqueio-{bloqueio.Id}",
            bloqueio.Id,
            bloqueio.Tipo == BloqueioCalendarioTipo.Manutencao ? "manutencao" : "bloqueio",
            bloqueio.Motivo,
            bloqueio.ImovelId,
            imovelNome,
            bloqueio.Inicio,
            bloqueio.Fim,
            bloqueio.Motivo,
            (int)bloqueio.Tipo);
    }
}

public sealed record BloqueioCalendarioRequest(
    int ImovelId,
    DateTime Inicio,
    DateTime Fim,
    BloqueioCalendarioTipo Tipo,
    string Motivo);

public sealed record CalendarioEventoResponse(
    string Id,
    int EntityId,
    string Tipo,
    string Titulo,
    int ImovelId,
    string ImovelNome,
    DateTime Inicio,
    DateTime Fim,
    string? Descricao,
    int Status);
