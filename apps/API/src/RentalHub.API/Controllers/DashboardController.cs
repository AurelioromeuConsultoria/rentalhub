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
public sealed class DashboardController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public DashboardController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("executivo")]
    public async Task<ActionResult<DashboardExecutivoResponse>> GetExecutivo(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var start = NormalizeDate(inicio ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc));
        var end = NormalizeDate(fim ?? now);

        if (end < start)
        {
            return BadRequest(new { message = "Período final deve ser maior ou igual ao período inicial." });
        }

        var movimentos = await _dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(m => m.Data >= start && m.Data <= end)
            .ToListAsync(cancellationToken);

        var receita = movimentos
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita)
            .Sum(m => m.Valor);

        var despesa = movimentos
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa)
            .Sum(m => m.Valor);

        var reservas = await _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Where(r => r.Status != ReservaStatus.Cancelada && r.CheckOut > start && r.CheckIn <= end)
            .ToListAsync(cancellationToken);

        var imoveisAtivos = await _dbContext.Imoveis
            .AsNoTracking()
            .Where(i => i.Status == ImovelStatus.Ativo)
            .Select(i => new { i.Id, i.Nome })
            .ToListAsync(cancellationToken);

        var reservasDoPeriodo = reservas.Count;
        var ticketMedio = reservasDoPeriodo == 0
            ? 0
            : reservas.Average(r => r.ValorHospedagem + r.TaxaLimpeza);

        var totalNoitesOcupadas = reservas.Sum(r => CountOverlapNights(r.CheckIn, r.CheckOut, start, end.AddDays(1)));
        var diasPeriodo = Math.Max(1, (end - start).Days + 1);
        var totalNoitesDisponiveis = Math.Max(1, imoveisAtivos.Count * diasPeriodo);
        var taxaOcupacao = Math.Round((decimal)totalNoitesOcupadas / totalNoitesDisponiveis * 100, 2);
        var fluxoDiario = Enumerable.Range(0, diasPeriodo)
            .Select(offset =>
            {
                var dia = start.AddDays(offset);
                var receitaDia = movimentos
                    .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita && m.Data.Date == dia)
                    .Sum(m => m.Valor);
                var despesaDia = movimentos
                    .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa && m.Data.Date == dia)
                    .Sum(m => m.Valor);

                return new DashboardFluxoDiarioResponse(dia, receitaDia, despesaDia, receitaDia - despesaDia);
            })
            .ToList();

        var reservasPorOrigem = reservas
            .GroupBy(r => r.Origem)
            .Select(group => new DashboardOrigemReservaResponse(
                group.Key.ToString(),
                group.Count(),
                group.Sum(r => r.ValorHospedagem + r.TaxaLimpeza)))
            .OrderByDescending(item => item.Quantidade)
            .ThenByDescending(item => item.Receita)
            .ToList();

        var repassesPendentes = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Where(r => r.Status == RepasseStatus.Pendente || r.Status == RepasseStatus.ParcialmentePago)
            .SumAsync(r => (decimal?)r.ValorRepassar - r.ValorPago, cancellationToken) ?? 0;

        var limpezasPendentes = await _dbContext.Limpezas
            .AsNoTracking()
            .CountAsync(l => l.Status == LimpezaStatus.Pendente || l.Status == LimpezaStatus.EmAndamento, cancellationToken);

        var manutencoesPendentes = await _dbContext.Manutencoes
            .AsNoTracking()
            .CountAsync(m => m.Status == ManutencaoStatus.Aberta || m.Status == ManutencaoStatus.EmAndamento, cancellationToken);

        var performance = imoveisAtivos
            .Select(imovel =>
            {
                var reservasImovel = reservas.Where(r => r.ImovelId == imovel.Id).ToArray();
                var despesasImovel = movimentos
                    .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa && m.ImovelId == imovel.Id)
                    .Sum(m => m.Valor);
                var receitaImovel = reservasImovel.Sum(r => r.ValorHospedagem + r.TaxaLimpeza);
                var taxas = reservasImovel.Sum(r => r.TaxaPlataforma);
                var comissao = reservasImovel.Sum(r => r.ComissaoAdministradora);
                var lucro = receitaImovel - despesasImovel - taxas - comissao;
                var noites = reservasImovel.Sum(r => CountOverlapNights(r.CheckIn, r.CheckOut, start, end.AddDays(1)));

                return new ImovelPerformanceResponse(
                    imovel.Id,
                    imovel.Nome,
                    receitaImovel,
                    despesasImovel,
                    lucro,
                    noites,
                    reservasImovel.Length);
            })
            .Where(item => item.Receita > 0 || item.Despesa > 0 || item.Reservas > 0)
            .ToList();

        return Ok(new DashboardExecutivoResponse(
            start,
            end,
            receita,
            despesa,
            receita - despesa,
            reservasDoPeriodo,
            taxaOcupacao,
            ticketMedio,
            imoveisAtivos.Count,
            repassesPendentes,
            limpezasPendentes,
            manutencoesPendentes,
            fluxoDiario,
            reservasPorOrigem,
            performance
                .OrderByDescending(i => i.Lucro)
                .ThenByDescending(i => i.Receita)
                .Take(5)
                .ToList(),
            performance
                .OrderBy(i => i.Lucro)
                .ThenBy(i => i.Receita)
                .Take(5)
                .ToList()));
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static int CountOverlapNights(DateTime checkIn, DateTime checkOut, DateTime start, DateTime endExclusive)
    {
        var overlapStart = checkIn > start ? checkIn : start;
        var overlapEnd = checkOut < endExclusive ? checkOut : endExclusive;

        return overlapEnd <= overlapStart ? 0 : Math.Max(0, (overlapEnd.Date - overlapStart.Date).Days);
    }
}

public sealed record DashboardExecutivoResponse(
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    decimal ReceitaMes,
    decimal DespesaMes,
    decimal LucroMes,
    int ReservasMes,
    decimal TaxaOcupacao,
    decimal TicketMedio,
    int ImoveisAtivos,
    decimal RepassesPendentes,
    int LimpezasPendentes,
    int ManutencoesPendentes,
    IReadOnlyCollection<DashboardFluxoDiarioResponse> FluxoDiario,
    IReadOnlyCollection<DashboardOrigemReservaResponse> ReservasPorOrigem,
    IReadOnlyCollection<ImovelPerformanceResponse> ImoveisMaisRentaveis,
    IReadOnlyCollection<ImovelPerformanceResponse> ImoveisMenorDesempenho);

public sealed record DashboardFluxoDiarioResponse(
    DateTime Data,
    decimal Receita,
    decimal Despesa,
    decimal Lucro);

public sealed record DashboardOrigemReservaResponse(
    string Origem,
    int Quantidade,
    decimal Receita);

public sealed record ImovelPerformanceResponse(
    int ImovelId,
    string ImovelNome,
    decimal Receita,
    decimal Despesa,
    decimal Lucro,
    int NoitesOcupadas,
    int Reservas);
