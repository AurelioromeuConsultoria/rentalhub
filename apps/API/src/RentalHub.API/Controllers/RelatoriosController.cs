using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class RelatoriosController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public RelatoriosController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("reservas")]
    public async Task<ActionResult<RelatorioReservasResponse>> Reservas(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] ReservaOrigem? plataforma,
        CancellationToken cancellationToken)
    {
        var period = GetPeriod(inicio, fim);
        if (period.Error is not null)
        {
            return period.Error;
        }

        var items = await GetReservasItemsAsync(period.Inicio, period.Fim, imovelId, plataforma, cancellationToken);
        return Ok(BuildReservasReport(period.Inicio, period.Fim, items));
    }

    [HttpGet("reservas.csv")]
    public async Task<IActionResult> ReservasCsv(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] ReservaOrigem? plataforma,
        CancellationToken cancellationToken)
    {
        var period = GetPeriod(inicio, fim);
        if (period.Error is not null)
        {
            return period.Error;
        }

        var report = BuildReservasReport(
            period.Inicio,
            period.Fim,
            await GetReservasItemsAsync(period.Inicio, period.Fim, imovelId, plataforma, cancellationToken));

        return Csv("relatorio-reservas.csv", report.Itens, [
            "Id", "Imovel", "Hospede", "Plataforma", "CheckIn", "CheckOut", "Status", "ValorHospedagem", "TaxaLimpeza", "ValorLiquido"
        ], item => [
            item.Id.ToString(CultureInfo.InvariantCulture),
            item.ImovelNome,
            item.HospedeNome,
            item.Plataforma,
            FormatDate(item.CheckIn),
            FormatDate(item.CheckOut),
            item.Status,
            FormatDecimal(item.ValorHospedagem),
            FormatDecimal(item.TaxaLimpeza),
            FormatDecimal(item.ValorLiquido)
        ]);
    }

    [HttpGet("financeiro")]
    public async Task<ActionResult<RelatorioFinanceiroResponse>> Financeiro(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? categoriaId,
        [FromQuery] int? imovelId,
        CancellationToken cancellationToken)
    {
        var period = GetPeriod(inicio, fim);
        if (period.Error is not null)
        {
            return period.Error;
        }

        var items = await GetFinanceiroItemsAsync(period.Inicio, period.Fim, categoriaId, imovelId, cancellationToken);
        return Ok(BuildFinanceiroReport(period.Inicio, period.Fim, items));
    }

    [HttpGet("financeiro.csv")]
    public async Task<IActionResult> FinanceiroCsv(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? categoriaId,
        [FromQuery] int? imovelId,
        CancellationToken cancellationToken)
    {
        var period = GetPeriod(inicio, fim);
        if (period.Error is not null)
        {
            return period.Error;
        }

        var report = BuildFinanceiroReport(
            period.Inicio,
            period.Fim,
            await GetFinanceiroItemsAsync(period.Inicio, period.Fim, categoriaId, imovelId, cancellationToken));

        return Csv("relatorio-financeiro.csv", report.Itens, [
            "Id", "Data", "Tipo", "Categoria", "Imovel", "Descricao", "Valor"
        ], item => [
            item.Id.ToString(CultureInfo.InvariantCulture),
            FormatDate(item.Data),
            item.Tipo,
            item.CategoriaNome,
            item.ImovelNome ?? string.Empty,
            item.Descricao,
            FormatDecimal(item.Valor)
        ]);
    }

    [HttpGet("imoveis")]
    public async Task<ActionResult<RelatorioImoveisResponse>> Imoveis(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        CancellationToken cancellationToken)
    {
        var period = GetPeriod(inicio, fim);
        if (period.Error is not null)
        {
            return period.Error;
        }

        return Ok(await BuildImoveisReportAsync(period.Inicio, period.Fim, imovelId, cancellationToken));
    }

    [HttpGet("imoveis.csv")]
    public async Task<IActionResult> ImoveisCsv(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        CancellationToken cancellationToken)
    {
        var period = GetPeriod(inicio, fim);
        if (period.Error is not null)
        {
            return period.Error;
        }

        var report = await BuildImoveisReportAsync(period.Inicio, period.Fim, imovelId, cancellationToken);
        return Csv("relatorio-imoveis.csv", report.Itens, [
            "ImovelId", "Imovel", "Receita", "Despesa", "Lucro", "Reservas", "NoitesOcupadas", "TaxaOcupacao"
        ], item => [
            item.ImovelId.ToString(CultureInfo.InvariantCulture),
            item.ImovelNome,
            FormatDecimal(item.Receita),
            FormatDecimal(item.Despesa),
            FormatDecimal(item.Lucro),
            item.Reservas.ToString(CultureInfo.InvariantCulture),
            item.NoitesOcupadas.ToString(CultureInfo.InvariantCulture),
            FormatDecimal(item.TaxaOcupacao)
        ]);
    }

    [HttpGet("proprietarios")]
    public async Task<ActionResult<RelatorioProprietariosResponse>> Proprietarios(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? proprietarioId,
        CancellationToken cancellationToken)
    {
        var period = GetPeriod(inicio, fim);
        if (period.Error is not null)
        {
            return period.Error;
        }

        return Ok(await BuildProprietariosReportAsync(period.Inicio, period.Fim, proprietarioId, cancellationToken));
    }

    [HttpGet("proprietarios.csv")]
    public async Task<IActionResult> ProprietariosCsv(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? proprietarioId,
        CancellationToken cancellationToken)
    {
        var period = GetPeriod(inicio, fim);
        if (period.Error is not null)
        {
            return period.Error;
        }

        var report = await BuildProprietariosReportAsync(period.Inicio, period.Fim, proprietarioId, cancellationToken);
        return Csv("relatorio-proprietarios.csv", report.Itens, [
            "ProprietarioId", "Proprietario", "Imoveis", "Reservas", "Receita", "Custos", "RepassesGerados", "RepassesPendentes"
        ], item => [
            item.ProprietarioId.ToString(CultureInfo.InvariantCulture),
            item.ProprietarioNome,
            item.TotalImoveis.ToString(CultureInfo.InvariantCulture),
            item.Reservas.ToString(CultureInfo.InvariantCulture),
            FormatDecimal(item.Receita),
            FormatDecimal(item.Custos),
            FormatDecimal(item.RepassesGerados),
            FormatDecimal(item.RepassesPendentes)
        ]);
    }

    [HttpGet("repasses/{id:int}/demonstrativo")]
    public async Task<ActionResult<DemonstrativoRepasseResponse>> DemonstrativoRepasse(int id, CancellationToken cancellationToken)
    {
        var repasse = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Include(r => r.Proprietario)
            .Include(r => r.Imovel)
            .Include(r => r.Itens)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (repasse is null)
        {
            return NotFound();
        }

        return Ok(new DemonstrativoRepasseResponse(
            repasse.Id,
            repasse.Proprietario?.Nome ?? string.Empty,
            repasse.Imovel?.Nome,
            repasse.PeriodoInicio,
            repasse.PeriodoFim,
            repasse.ReceitaReservas,
            repasse.TaxasPlataforma,
            repasse.CustosVinculados,
            repasse.ComissaoAdministradora,
            repasse.ValorRepassar,
            repasse.ValorPago,
            repasse.ValorRepassar - repasse.ValorPago,
            repasse.Status.ToString(),
            repasse.Itens
                .OrderBy(i => i.Id)
                .Select(i => new DemonstrativoRepasseItemResponse(
                    i.Descricao,
                    i.Receita,
                    i.Taxas,
                    i.Custos,
                    i.Comissao,
                    i.ValorLiquido))
                .ToList()));
    }

    private async Task<List<RelatorioReservaItemResponse>> GetReservasItemsAsync(
        DateTime inicio,
        DateTime fim,
        int? imovelId,
        ReservaOrigem? plataforma,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .Where(r => r.Status != ReservaStatus.Cancelada && r.CheckOut >= inicio && r.CheckIn <= fim);

        if (imovelId.HasValue)
        {
            query = query.Where(r => r.ImovelId == imovelId.Value);
        }

        if (plataforma.HasValue)
        {
            query = query.Where(r => r.Origem == plataforma.Value);
        }

        return await query
            .OrderBy(r => r.CheckIn)
            .Select(r => new RelatorioReservaItemResponse(
                r.Id,
                r.ImovelId,
                r.Imovel == null ? string.Empty : r.Imovel.Nome,
                r.Hospede == null ? string.Empty : r.Hospede.Nome,
                r.Origem.ToString(),
                r.CheckIn,
                r.CheckOut,
                r.Status.ToString(),
                r.ValorHospedagem,
                r.TaxaLimpeza,
                r.TaxaPlataforma,
                r.ComissaoAdministradora,
                r.ValorLiquido))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<RelatorioFinanceiroItemResponse>> GetFinanceiroItemsAsync(
        DateTime inicio,
        DateTime fim,
        int? categoriaId,
        int? imovelId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Include(m => m.CategoriaFinanceira)
            .Include(m => m.Imovel)
            .Where(m => m.Data >= inicio && m.Data <= fim);

        if (categoriaId.HasValue)
        {
            query = query.Where(m => m.CategoriaFinanceiraId == categoriaId.Value);
        }

        if (imovelId.HasValue)
        {
            query = query.Where(m => m.ImovelId == imovelId.Value);
        }

        return await query
            .OrderBy(m => m.Data)
            .Select(m => new RelatorioFinanceiroItemResponse(
                m.Id,
                m.Data,
                m.Tipo.ToString(),
                m.CategoriaFinanceira == null ? string.Empty : m.CategoriaFinanceira.Nome,
                m.Imovel == null ? null : m.Imovel.Nome,
                m.Descricao,
                m.Valor))
            .ToListAsync(cancellationToken);
    }

    private static RelatorioReservasResponse BuildReservasReport(
        DateTime inicio,
        DateTime fim,
        IReadOnlyCollection<RelatorioReservaItemResponse> items)
    {
        return new RelatorioReservasResponse(
            inicio,
            fim,
            items.Count,
            items.Sum(i => i.ValorHospedagem),
            items.Sum(i => i.TaxaLimpeza),
            items.Sum(i => i.TaxaPlataforma),
            items.Sum(i => i.ComissaoAdministradora),
            items.Sum(i => i.ValorLiquido),
            items
                .GroupBy(i => i.Plataforma)
                .Select(g => new RelatorioGrupoResponse(g.Key, g.Count(), g.Sum(i => i.ValorLiquido)))
                .OrderByDescending(g => g.Total)
                .ToList(),
            items);
    }

    private static RelatorioFinanceiroResponse BuildFinanceiroReport(
        DateTime inicio,
        DateTime fim,
        IReadOnlyCollection<RelatorioFinanceiroItemResponse> items)
    {
        var receitas = items.Where(i => i.Tipo == MovimentacaoFinanceiraTipo.Receita.ToString()).Sum(i => i.Valor);
        var despesas = items.Where(i => i.Tipo == MovimentacaoFinanceiraTipo.Despesa.ToString()).Sum(i => i.Valor);

        return new RelatorioFinanceiroResponse(
            inicio,
            fim,
            receitas,
            despesas,
            receitas - despesas,
            items
                .GroupBy(i => new { i.Tipo, i.CategoriaNome })
                .Select(g => new RelatorioFinanceiroCategoriaResponse(g.Key.Tipo, g.Key.CategoriaNome, g.Sum(i => i.Valor)))
                .OrderBy(g => g.Tipo)
                .ThenByDescending(g => g.Total)
                .ToList(),
            items);
    }

    private async Task<RelatorioImoveisResponse> BuildImoveisReportAsync(
        DateTime inicio,
        DateTime fim,
        int? imovelId,
        CancellationToken cancellationToken)
    {
        var imoveis = await _dbContext.Imoveis.AsNoTracking()
            .Where(i => !imovelId.HasValue || i.Id == imovelId.Value)
            .Select(i => new { i.Id, i.Nome })
            .ToListAsync(cancellationToken);

        var reservas = await _dbContext.Reservas.AsNoTracking()
            .Where(r => r.Status != ReservaStatus.Cancelada && r.CheckOut >= inicio && r.CheckIn <= fim)
            .ToListAsync(cancellationToken);

        var movimentos = await _dbContext.MovimentacoesFinanceiras.AsNoTracking()
            .Where(m => m.Data >= inicio && m.Data <= fim)
            .ToListAsync(cancellationToken);

        var diasPeriodo = Math.Max(1, (fim - inicio).Days + 1);
        var items = imoveis.Select(imovel =>
        {
            var reservasImovel = reservas.Where(r => r.ImovelId == imovel.Id).ToArray();
            var receita = reservasImovel.Sum(r => r.ValorHospedagem + r.TaxaLimpeza);
            var despesa = movimentos.Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa && m.ImovelId == imovel.Id).Sum(m => m.Valor);
            var taxas = reservasImovel.Sum(r => r.TaxaPlataforma + r.ComissaoAdministradora);
            var noites = reservasImovel.Sum(r => CountOverlapNights(r.CheckIn, r.CheckOut, inicio, fim.AddDays(1)));

            return new RelatorioImovelItemResponse(
                imovel.Id,
                imovel.Nome,
                receita,
                despesa,
                receita - despesa - taxas,
                reservasImovel.Length,
                noites,
                Math.Round((decimal)noites / diasPeriodo * 100, 2));
        }).ToList();

        return new RelatorioImoveisResponse(
            inicio,
            fim,
            items.Sum(i => i.Receita),
            items.Sum(i => i.Despesa),
            items.Sum(i => i.Lucro),
            items);
    }

    private async Task<RelatorioProprietariosResponse> BuildProprietariosReportAsync(
        DateTime inicio,
        DateTime fim,
        int? proprietarioId,
        CancellationToken cancellationToken)
    {
        var proprietarios = await _dbContext.Proprietarios
            .AsNoTracking()
            .Include(p => p.Imoveis)
            .Where(p => !proprietarioId.HasValue || p.Id == proprietarioId.Value)
            .ToListAsync(cancellationToken);

        var reservas = await _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Where(r => r.Status != ReservaStatus.Cancelada && r.CheckOut >= inicio && r.CheckIn <= fim)
            .ToListAsync(cancellationToken);

        var movimentos = await _dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(m => m.Data >= inicio && m.Data <= fim)
            .ToListAsync(cancellationToken);

        var repasses = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Where(r => r.PeriodoFim >= inicio && r.PeriodoInicio <= fim)
            .ToListAsync(cancellationToken);

        var items = proprietarios.Select(proprietario =>
        {
            var imovelIds = proprietario.Imoveis.Select(i => i.Id).ToArray();
            var reservasProprietario = reservas.Where(r => imovelIds.Contains(r.ImovelId)).ToArray();
            var receita = reservasProprietario.Sum(r => r.ValorHospedagem + r.TaxaLimpeza);
            var custos = movimentos
                .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa &&
                    ((m.ProprietarioId.HasValue && m.ProprietarioId.Value == proprietario.Id) ||
                     (m.ImovelId.HasValue && imovelIds.Contains(m.ImovelId.Value))))
                .Sum(m => m.Valor);
            var repassesProprietario = repasses.Where(r => r.ProprietarioId == proprietario.Id).ToArray();

            return new RelatorioProprietarioItemResponse(
                proprietario.Id,
                proprietario.Nome,
                imovelIds.Length,
                reservasProprietario.Length,
                receita,
                custos,
                repassesProprietario.Sum(r => r.ValorRepassar),
                repassesProprietario.Sum(r => r.ValorRepassar - r.ValorPago));
        }).ToList();

        return new RelatorioProprietariosResponse(
            inicio,
            fim,
            items.Sum(i => i.Receita),
            items.Sum(i => i.Custos),
            items.Sum(i => i.RepassesGerados),
            items.Sum(i => i.RepassesPendentes),
            items);
    }

    private static (DateTime Inicio, DateTime Fim, ActionResult? Error) GetPeriod(DateTime? inicio, DateTime? fim)
    {
        var now = DateTime.UtcNow;
        var start = NormalizeDate(inicio ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc));
        var end = NormalizeDate(fim ?? now);

        return end < start
            ? (start, end, new BadRequestObjectResult(new { message = "Período final deve ser maior ou igual ao período inicial." }))
            : (start, end, null);
    }

    private static IActionResult Csv<T>(
        string fileName,
        IEnumerable<T> items,
        IReadOnlyCollection<string> headers,
        Func<T, IReadOnlyCollection<string>> rowFactory)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(";", headers.Select(EscapeCsv)));
        foreach (var item in items)
        {
            builder.AppendLine(string.Join(";", rowFactory(item).Select(EscapeCsv)));
        }

        return new FileContentResult(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv; charset=utf-8")
        {
            FileDownloadName = fileName
        };
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return escaped.Contains(';') || escaped.Contains('"') || escaped.Contains('\n')
            ? $"\"{escaped}\""
            : escaped;
    }

    private static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

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

public sealed record RelatorioGrupoResponse(string Nome, int Quantidade, decimal Total);

public sealed record RelatorioReservasResponse(
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    int TotalReservas,
    decimal ValorHospedagem,
    decimal TaxaLimpeza,
    decimal TaxaPlataforma,
    decimal ComissaoAdministradora,
    decimal ValorLiquido,
    IReadOnlyCollection<RelatorioGrupoResponse> PorPlataforma,
    IReadOnlyCollection<RelatorioReservaItemResponse> Itens);

public sealed record RelatorioReservaItemResponse(
    int Id,
    int ImovelId,
    string ImovelNome,
    string HospedeNome,
    string Plataforma,
    DateTime CheckIn,
    DateTime CheckOut,
    string Status,
    decimal ValorHospedagem,
    decimal TaxaLimpeza,
    decimal TaxaPlataforma,
    decimal ComissaoAdministradora,
    decimal ValorLiquido);

public sealed record RelatorioFinanceiroResponse(
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    decimal Receitas,
    decimal Despesas,
    decimal Lucro,
    IReadOnlyCollection<RelatorioFinanceiroCategoriaResponse> PorCategoria,
    IReadOnlyCollection<RelatorioFinanceiroItemResponse> Itens);

public sealed record RelatorioFinanceiroCategoriaResponse(string Tipo, string CategoriaNome, decimal Total);

public sealed record RelatorioFinanceiroItemResponse(
    int Id,
    DateTime Data,
    string Tipo,
    string CategoriaNome,
    string? ImovelNome,
    string Descricao,
    decimal Valor);

public sealed record RelatorioImoveisResponse(
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    decimal Receita,
    decimal Despesa,
    decimal Lucro,
    IReadOnlyCollection<RelatorioImovelItemResponse> Itens);

public sealed record RelatorioImovelItemResponse(
    int ImovelId,
    string ImovelNome,
    decimal Receita,
    decimal Despesa,
    decimal Lucro,
    int Reservas,
    int NoitesOcupadas,
    decimal TaxaOcupacao);

public sealed record RelatorioProprietariosResponse(
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    decimal Receita,
    decimal Custos,
    decimal RepassesGerados,
    decimal RepassesPendentes,
    IReadOnlyCollection<RelatorioProprietarioItemResponse> Itens);

public sealed record RelatorioProprietarioItemResponse(
    int ProprietarioId,
    string ProprietarioNome,
    int TotalImoveis,
    int Reservas,
    decimal Receita,
    decimal Custos,
    decimal RepassesGerados,
    decimal RepassesPendentes);

public sealed record DemonstrativoRepasseResponse(
    int RepasseId,
    string ProprietarioNome,
    string? ImovelNome,
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    decimal ReceitaReservas,
    decimal TaxasPlataforma,
    decimal CustosVinculados,
    decimal ComissaoAdministradora,
    decimal ValorRepassar,
    decimal ValorPago,
    decimal SaldoPendente,
    string Status,
    IReadOnlyCollection<DemonstrativoRepasseItemResponse> Itens);

public sealed record DemonstrativoRepasseItemResponse(
    string Descricao,
    decimal Receita,
    decimal Taxas,
    decimal Custos,
    decimal Comissao,
    decimal ValorLiquido);
