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
        var demonstrativo = await GetDemonstrativoRepasseAsync(id, cancellationToken);
        return demonstrativo is null ? NotFound() : Ok(demonstrativo);
    }

    [HttpGet("repasses/{id:int}/demonstrativo.pdf")]
    public async Task<IActionResult> DemonstrativoRepassePdf(int id, CancellationToken cancellationToken)
    {
        var demonstrativo = await GetDemonstrativoRepasseAsync(id, cancellationToken);
        if (demonstrativo is null)
        {
            return NotFound();
        }

        var pdf = BuildDemonstrativoRepassePdf(demonstrativo);
        return File(pdf, "application/pdf", $"demonstrativo-repasse-{id}.pdf");
    }

    private async Task<DemonstrativoRepasseResponse?> GetDemonstrativoRepasseAsync(int id, CancellationToken cancellationToken)
    {
        var repasse = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Include(r => r.Proprietario)
            .Include(r => r.Imovel)
            .Include(r => r.Itens)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (repasse is null)
        {
            return null;
        }

        return new DemonstrativoRepasseResponse(
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
                .ToList());
    }

    private static byte[] BuildDemonstrativoRepassePdf(DemonstrativoRepasseResponse demonstrativo)
    {
        var lines = new List<string>
        {
            "RentalHub - Demonstrativo de Repasse",
            $"Repasse #{demonstrativo.RepasseId}",
            $"Proprietario: {demonstrativo.ProprietarioNome}",
            $"Imovel: {demonstrativo.ImovelNome ?? "Todos os imoveis"}",
            $"Periodo: {FormatDate(demonstrativo.PeriodoInicio)} a {FormatDate(demonstrativo.PeriodoFim)}",
            $"Status: {demonstrativo.Status}",
            string.Empty,
            $"Receitas: {FormatCurrency(demonstrativo.ReceitaReservas)}",
            $"Taxas da plataforma: {FormatCurrency(demonstrativo.TaxasPlataforma)}",
            $"Custos vinculados: {FormatCurrency(demonstrativo.CustosVinculados)}",
            $"Comissao da administradora: {FormatCurrency(demonstrativo.ComissaoAdministradora)}",
            $"Valor a repassar: {FormatCurrency(demonstrativo.ValorRepassar)}",
            $"Valor pago: {FormatCurrency(demonstrativo.ValorPago)}",
            $"Saldo pendente: {FormatCurrency(demonstrativo.SaldoPendente)}",
            string.Empty,
            "Itens",
            "Descricao | Receita | Taxas | Custos | Comissao | Liquido"
        };

        lines.AddRange(demonstrativo.Itens.Select(item =>
            $"{item.Descricao} | {FormatCurrency(item.Receita)} | {FormatCurrency(item.Taxas)} | {FormatCurrency(item.Custos)} | {FormatCurrency(item.Comissao)} | {FormatCurrency(item.ValorLiquido)}"));

        lines.Add(string.Empty);
        lines.Add($"Emitido em {FormatDate(DateTime.UtcNow)}");

        return SimplePdf.Create(lines);
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

    private static string FormatCurrency(decimal value) => $"R$ {FormatDecimal(value)}";

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

    private static class SimplePdf
    {
        private const int LinesPerPage = 44;
        private const int MaxLineLength = 105;

        public static byte[] Create(IReadOnlyCollection<string> rawLines)
        {
            var lines = rawLines.SelectMany(WrapLine).ToList();
            var pages = lines.Chunk(LinesPerPage).Select(chunk => chunk.ToArray()).ToList();
            if (pages.Count == 0)
            {
                pages.Add([]);
            }

            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                string.Empty,
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
            };

            var pageObjectIds = new List<int>();
            foreach (var pageLines in pages)
            {
                var pageObjectId = objects.Count + 1;
                var contentObjectId = pageObjectId + 1;
                pageObjectIds.Add(pageObjectId);

                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectId} 0 R >>");
                objects.Add(CreateContentObject(pageLines));
            }

            objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>";

            var builder = new StringBuilder();
            var offsets = new List<int> { 0 };
            builder.Append("%PDF-1.4\n");

            for (var index = 0; index < objects.Count; index++)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
                builder.Append(index + 1).Append(" 0 obj\n")
                    .Append(objects[index]).Append("\nendobj\n");
            }

            var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.Append("xref\n")
                .Append("0 ").Append(objects.Count + 1).Append('\n')
                .Append("0000000000 65535 f \n");

            foreach (var offset in offsets.Skip(1))
            {
                builder.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            }

            builder.Append("trailer\n")
                .Append("<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n")
                .Append("startxref\n")
                .Append(xrefOffset).Append('\n')
                .Append("%%EOF");

            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static string CreateContentObject(IReadOnlyCollection<string> lines)
        {
            var content = new StringBuilder();
            var y = 800;
            foreach (var line in lines)
            {
                var fontSize = y == 800 ? 14 : 9;
                content.Append("BT /F1 ").Append(fontSize).Append(" Tf 42 ")
                    .Append(y.ToString(CultureInfo.InvariantCulture))
                    .Append(" Td (").Append(EscapePdfText(line)).Append(") Tj ET\n");
                y -= y == 800 ? 22 : 15;
            }

            var stream = content.ToString();
            return $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream";
        }

        private static IEnumerable<string> WrapLine(string line)
        {
            var text = ToAscii(line);
            if (text.Length <= MaxLineLength)
            {
                yield return text;
                yield break;
            }

            for (var index = 0; index < text.Length; index += MaxLineLength)
            {
                yield return text.Substring(index, Math.Min(MaxLineLength, text.Length - index));
            }
        }

        private static string ToAscii(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(character <= 127 ? character : '?');
            }

            return builder.ToString();
        }

        private static string EscapePdfText(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }
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
