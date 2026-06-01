using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class PortalProprietarioController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public PortalProprietarioController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PortalProprietarioResponse>> GetPortal(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        CancellationToken cancellationToken)
    {
        var proprietarioId = GetProprietarioId();
        if (!proprietarioId.HasValue)
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var start = NormalizeDate(inicio ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc));
        var end = NormalizeDate(fim ?? now);
        if (end < start)
        {
            return BadRequest(new { message = "Período final deve ser maior ou igual ao período inicial." });
        }

        var proprietario = await _dbContext.Proprietarios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proprietarioId.Value && p.Ativo, cancellationToken);

        if (proprietario is null)
        {
            return Forbid();
        }

        var imoveis = await _dbContext.Imoveis
            .AsNoTracking()
            .Where(i => i.ProprietarioId == proprietarioId.Value)
            .OrderBy(i => i.Nome)
            .Select(i => new PortalImovelResponse(
                i.Id,
                i.Nome,
                i.CodigoInterno,
                i.Cidade,
                i.Estado,
                i.Status.ToString()))
            .ToListAsync(cancellationToken);

        var imovelIds = imoveis.Select(i => i.Id).ToArray();

        var reservas = await _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .Where(r =>
                imovelIds.Contains(r.ImovelId) &&
                r.Status != ReservaStatus.Cancelada &&
                r.CheckOut >= start &&
                r.CheckIn <= end)
            .OrderBy(r => r.CheckIn)
            .Select(r => new PortalReservaResponse(
                r.Id,
                r.ImovelId,
                r.Imovel == null ? string.Empty : r.Imovel.Nome,
                r.Hospede == null ? string.Empty : r.Hospede.Nome,
                r.CheckIn,
                r.CheckOut,
                r.Origem.ToString(),
                r.Status.ToString(),
                r.ValorHospedagem + r.TaxaLimpeza,
                r.ValorLiquido))
            .ToListAsync(cancellationToken);

        var movimentacoes = await _dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Include(m => m.Imovel)
            .Include(m => m.CategoriaFinanceira)
            .Where(m =>
                m.Data >= start &&
                m.Data <= end &&
                (m.ProprietarioId == proprietarioId.Value ||
                 (m.ImovelId.HasValue && imovelIds.Contains(m.ImovelId.Value))))
            .OrderByDescending(m => m.Data)
            .Select(m => new PortalMovimentacaoResponse(
                m.Id,
                m.Data,
                m.Tipo.ToString(),
                m.CategoriaFinanceira == null ? string.Empty : m.CategoriaFinanceira.Nome,
                m.Imovel == null ? null : m.Imovel.Nome,
                m.Descricao,
                m.Valor))
            .ToListAsync(cancellationToken);

        var repasses = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Where(r =>
                r.ProprietarioId == proprietarioId.Value &&
                r.PeriodoFim >= start &&
                r.PeriodoInicio <= end)
            .OrderByDescending(r => r.PeriodoFim)
            .Select(r => new PortalRepasseResponse(
                r.Id,
                r.PeriodoInicio,
                r.PeriodoFim,
                r.ValorRepassar,
                r.ValorPago,
                r.ValorRepassar - r.ValorPago,
                r.Status.ToString()))
            .ToListAsync(cancellationToken);

        var calendario = reservas
            .Select(r => new PortalCalendarioEventoResponse(
                $"reserva-{r.Id}",
                "reserva",
                r.ImovelNome,
                r.CheckIn,
                r.CheckOut,
                r.Status))
            .Concat(repasses.Select(r => new PortalCalendarioEventoResponse(
                $"repasse-{r.Id}",
                "repasse",
                "Repasse",
                r.PeriodoFim,
                r.PeriodoFim.AddDays(1),
                r.Status)))
            .OrderBy(e => e.Inicio)
            .ToList();

        var receitas = movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita.ToString())
            .Sum(m => m.Valor);

        var custos = movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa.ToString())
            .Sum(m => m.Valor);

        return Ok(new PortalProprietarioResponse(
            proprietario.Id,
            proprietario.Nome,
            start,
            end,
            imoveis.Count,
            reservas.Count,
            receitas,
            custos,
            repasses.Sum(r => r.ValorRepassar),
            repasses.Sum(r => r.SaldoPendente),
            imoveis,
            reservas,
            movimentacoes,
            repasses,
            calendario));
    }

    private int? GetProprietarioId()
    {
        return int.TryParse(User.FindFirstValue("ProprietarioId"), out var proprietarioId)
            ? proprietarioId
            : null;
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }
}

public sealed record PortalProprietarioResponse(
    int ProprietarioId,
    string ProprietarioNome,
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    int TotalImoveis,
    int TotalReservas,
    decimal Receitas,
    decimal Custos,
    decimal RepassesGerados,
    decimal RepassesPendentes,
    IReadOnlyCollection<PortalImovelResponse> Imoveis,
    IReadOnlyCollection<PortalReservaResponse> Reservas,
    IReadOnlyCollection<PortalMovimentacaoResponse> Movimentacoes,
    IReadOnlyCollection<PortalRepasseResponse> Repasses,
    IReadOnlyCollection<PortalCalendarioEventoResponse> Calendario);

public sealed record PortalImovelResponse(
    int Id,
    string Nome,
    string CodigoInterno,
    string? Cidade,
    string? Estado,
    string Status);

public sealed record PortalReservaResponse(
    int Id,
    int ImovelId,
    string ImovelNome,
    string HospedeNome,
    DateTime CheckIn,
    DateTime CheckOut,
    string Origem,
    string Status,
    decimal Receita,
    decimal ValorLiquido);

public sealed record PortalMovimentacaoResponse(
    int Id,
    DateTime Data,
    string Tipo,
    string CategoriaNome,
    string? ImovelNome,
    string Descricao,
    decimal Valor);

public sealed record PortalRepasseResponse(
    int Id,
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    decimal ValorRepassar,
    decimal ValorPago,
    decimal SaldoPendente,
    string Status);

public sealed record PortalCalendarioEventoResponse(
    string Id,
    string Tipo,
    string Titulo,
    DateTime Inicio,
    DateTime Fim,
    string Status);
