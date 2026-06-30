using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.API.Services;
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
    public async Task<IActionResult> GetPortal(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] string? reservaStatus,
        [FromQuery] string? origem,
        CancellationToken cancellationToken)
    {
        var result = await LoadPortalDataAsync(inicio, fim, imovelId, reservaStatus, origem, cancellationToken);
        var error = ToErrorResult(result);
        if (error is not null)
        {
            return error;
        }

        return Ok(result.Data);
    }

    [HttpGet("reservas.pdf")]
    public async Task<IActionResult> ReservasPdf(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] string? reservaStatus,
        [FromQuery] string? origem,
        CancellationToken cancellationToken)
    {
        var result = await LoadPortalDataAsync(inicio, fim, imovelId, reservaStatus, origem, cancellationToken);
        var error = ToErrorResult(result);
        if (error is not null)
        {
            return error;
        }

        var data = result.Data!;
        var pdf = BuildReservasPdf(data);
        return File(pdf, "application/pdf", $"portal-reservas-{FormatDateForFile(data.PeriodoInicio)}-{FormatDateForFile(data.PeriodoFim)}.pdf");
    }

    [HttpGet("movimentacoes.pdf")]
    public async Task<IActionResult> MovimentacoesPdf(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] string? reservaStatus,
        [FromQuery] string? origem,
        CancellationToken cancellationToken)
    {
        var result = await LoadPortalDataAsync(inicio, fim, imovelId, reservaStatus, origem, cancellationToken);
        var error = ToErrorResult(result);
        if (error is not null)
        {
            return error;
        }

        var data = result.Data!;
        var pdf = BuildMovimentacoesPdf(data);
        return File(pdf, "application/pdf", $"portal-movimentacoes-{FormatDateForFile(data.PeriodoInicio)}-{FormatDateForFile(data.PeriodoFim)}.pdf");
    }

    [HttpGet("repasses.pdf")]
    public async Task<IActionResult> RepassesPdf(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] string? reservaStatus,
        [FromQuery] string? origem,
        CancellationToken cancellationToken)
    {
        var result = await LoadPortalDataAsync(inicio, fim, imovelId, reservaStatus, origem, cancellationToken);
        var error = ToErrorResult(result);
        if (error is not null)
        {
            return error;
        }

        var data = result.Data!;
        var pdf = BuildRepassesPdf(data);
        return File(pdf, "application/pdf", $"portal-repasses-{FormatDateForFile(data.PeriodoInicio)}-{FormatDateForFile(data.PeriodoFim)}.pdf");
    }

    [HttpGet("demonstrativo-mensal.pdf")]
    public async Task<IActionResult> DemonstrativoMensalPdf(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] string? reservaStatus,
        [FromQuery] string? origem,
        CancellationToken cancellationToken)
    {
        var result = await LoadPortalDataAsync(inicio, fim, imovelId, reservaStatus, origem, cancellationToken);
        var error = ToErrorResult(result);
        if (error is not null)
        {
            return error;
        }

        var data = result.Data!;
        var pdf = BuildDemonstrativoMensalPdf(data);
        return File(pdf, "application/pdf", $"demonstrativo-mensal-{FormatDateForFile(data.PeriodoInicio)}-{FormatDateForFile(data.PeriodoFim)}.pdf");
    }

    [HttpGet("reservas/{id:int}/detalhe.pdf")]
    public async Task<IActionResult> ReservaDetalhePdf(int id, CancellationToken cancellationToken)
    {
        var proprietarioId = GetProprietarioId();
        if (!proprietarioId.HasValue)
        {
            return Forbid();
        }

        var reserva = await _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .FirstOrDefaultAsync(r => r.Id == id && r.Imovel != null && r.Imovel.ProprietarioId == proprietarioId.Value, cancellationToken);

        if (reserva is null)
        {
            return NotFound();
        }

        var pdf = BuildReservaDetalhePdf(reserva);
        return File(pdf, "application/pdf", $"reserva-{id}.pdf");
    }

    [HttpGet("repasses/{id:int}")]
    public async Task<ActionResult<PortalRepasseDetalheResponse>> RepasseDetalhe(int id, CancellationToken cancellationToken)
    {
        var proprietarioId = GetProprietarioId();
        if (!proprietarioId.HasValue)
        {
            return Forbid();
        }

        var repasse = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Itens)
            .FirstOrDefaultAsync(r => r.Id == id && r.ProprietarioId == proprietarioId.Value, cancellationToken);

        if (repasse is null)
        {
            return NotFound();
        }

        return Ok(new PortalRepasseDetalheResponse(
            repasse.Id,
            repasse.ImovelId,
            repasse.Imovel?.Nome,
            repasse.PeriodoInicio,
            repasse.PeriodoFim,
            repasse.ReceitaReservas,
            repasse.TaxasPlataforma,
            repasse.CustosVinculados,
            repasse.ComissaoAdministradora,
            repasse.PercentualSocio,
            repasse.ValorRepassar,
            repasse.ValorPago,
            repasse.ValorRepassar - repasse.ValorPago,
            repasse.Status.ToString(),
            repasse.DataPagamento,
            repasse.Observacoes,
            repasse.Itens
                .OrderBy(i => i.Id)
                .Select(i => new PortalRepasseItemResponse(
                    i.Id,
                    i.ReservaId,
                    i.MovimentacaoFinanceiraId,
                    i.Descricao,
                    i.Receita,
                    i.Taxas,
                    i.Custos,
                    i.Comissao,
                    i.ValorLiquido))
                .ToList()));
    }

    [HttpGet("repasses/{id:int}/demonstrativo.pdf")]
    public async Task<IActionResult> DemonstrativoRepassePdf(int id, CancellationToken cancellationToken)
    {
        var proprietarioId = GetProprietarioId();
        if (!proprietarioId.HasValue)
        {
            return Forbid();
        }

        var repasse = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Include(r => r.Proprietario)
            .Include(r => r.Imovel)
            .Include(r => r.Itens)
            .FirstOrDefaultAsync(r => r.Id == id && r.ProprietarioId == proprietarioId.Value, cancellationToken);

        if (repasse is null)
        {
            return NotFound();
        }

        var pdf = BuildDemonstrativoRepassePdf(repasse);
        return File(pdf, "application/pdf", $"demonstrativo-repasse-{id}.pdf");
    }

    private async Task<PortalDataResult> LoadPortalDataAsync(
        DateTime? inicio,
        DateTime? fim,
        int? imovelId,
        string? reservaStatus,
        string? origem,
        CancellationToken cancellationToken)
    {
        var proprietarioId = GetProprietarioId();
        if (!proprietarioId.HasValue)
        {
            return PortalDataResult.Forbidden();
        }

        var now = DateTime.UtcNow;
        var start = NormalizeDate(inicio ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc));
        var end = NormalizeDate(fim ?? now);
        if (end < start)
        {
            return PortalDataResult.Invalid("Período final deve ser maior ou igual ao período inicial.");
        }

        var reservaStatusFilter = ParseEnumFilter<ReservaStatus>(reservaStatus, "status da reserva");
        if (reservaStatusFilter.ErrorMessage is not null)
        {
            return PortalDataResult.Invalid(reservaStatusFilter.ErrorMessage);
        }

        var origemFilter = ParseEnumFilter<ReservaOrigem>(origem, "origem da reserva");
        if (origemFilter.ErrorMessage is not null)
        {
            return PortalDataResult.Invalid(origemFilter.ErrorMessage);
        }

        var proprietario = await _dbContext.Proprietarios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proprietarioId.Value && p.Ativo, cancellationToken);

        if (proprietario is null)
        {
            return PortalDataResult.Forbidden();
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
                i.Status.ToString(),
                i.QuantidadeHospedes,
                i.QuantidadeQuartos,
                i.QuantidadeBanheiros,
                i.PercentualRepasseSocio ?? 100,
                i.Fotos
                    .OrderByDescending(f => f.Principal)
                    .ThenBy(f => f.Ordem)
                    .Select(f => f.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var imovelIds = imoveis.Select(i => i.Id).ToArray();
        if (imovelId.HasValue && !imovelIds.Contains(imovelId.Value))
        {
            return PortalDataResult.Invalid("Imóvel não pertence ao sócio autenticado.");
        }

        var selectedImovelIds = imovelId.HasValue ? [imovelId.Value] : imovelIds;

        var reservasQuery = _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .Where(r =>
                selectedImovelIds.Contains(r.ImovelId) &&
                (reservaStatusFilter.Value == ReservaStatus.Cancelada || r.Status != ReservaStatus.Cancelada) &&
                r.CheckOut >= start &&
                r.CheckIn <= end);

        if (reservaStatusFilter.Value.HasValue)
        {
            reservasQuery = reservasQuery.Where(r => r.Status == reservaStatusFilter.Value.Value);
        }

        if (origemFilter.Value.HasValue)
        {
            reservasQuery = reservasQuery.Where(r => r.Origem == origemFilter.Value.Value);
        }

        var reservas = await reservasQuery
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
                ((!imovelId.HasValue && m.ProprietarioId == proprietarioId.Value) ||
                 (m.ImovelId.HasValue && selectedImovelIds.Contains(m.ImovelId.Value))))
            .OrderByDescending(m => m.Data)
            .Select(m => new PortalMovimentacaoResponse(
                m.Id,
                m.ImovelId,
                m.Data,
                m.Tipo.ToString(),
                m.CategoriaFinanceira == null ? string.Empty : m.CategoriaFinanceira.Nome,
                m.Imovel == null ? null : m.Imovel.Nome,
                m.Descricao,
                m.Valor))
            .ToListAsync(cancellationToken);

        var repasses = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Where(r =>
                r.ProprietarioId == proprietarioId.Value &&
                (!imovelId.HasValue || r.ImovelId == imovelId.Value) &&
                r.PeriodoFim >= start &&
                r.PeriodoInicio <= end)
            .OrderByDescending(r => r.PeriodoFim)
            .Select(r => new PortalRepasseResponse(
                r.Id,
                r.ImovelId,
                r.Imovel == null ? null : r.Imovel.Nome,
                r.PeriodoInicio,
                r.PeriodoFim,
                r.PercentualSocio,
                r.ValorRepassar,
                r.ValorPago,
                r.ValorRepassar - r.ValorPago,
                r.Status.ToString()))
            .ToListAsync(cancellationToken);

        var bloqueios = await _dbContext.BloqueiosCalendario
            .AsNoTracking()
            .Include(b => b.Imovel)
            .Where(b =>
                selectedImovelIds.Contains(b.ImovelId) &&
                b.Fim >= start &&
                b.Inicio <= end)
            .OrderBy(b => b.Inicio)
            .Select(b => new PortalCalendarioEventoResponse(
                $"bloqueio-{b.Id}",
                b.ImovelId,
                b.Imovel == null ? null : b.Imovel.Nome,
                b.Tipo == BloqueioCalendarioTipo.Manutencao ? "manutencao" : "bloqueio",
                string.IsNullOrWhiteSpace(b.Motivo) ? b.Tipo.ToString() : b.Motivo,
                b.Inicio,
                b.Fim,
                b.Tipo.ToString()))
            .ToListAsync(cancellationToken);

        var manutencoes = await _dbContext.Manutencoes
            .AsNoTracking()
            .Include(m => m.Imovel)
            .Where(m =>
                selectedImovelIds.Contains(m.ImovelId) &&
                m.Status != ManutencaoStatus.Cancelada &&
                ((m.DataPrevista.HasValue && m.DataPrevista.Value >= start && m.DataPrevista.Value <= end) ||
                 (!m.DataPrevista.HasValue && m.DataAbertura >= start && m.DataAbertura <= end)))
            .OrderBy(m => m.DataPrevista ?? m.DataAbertura)
            .Select(m => new PortalCalendarioEventoResponse(
                $"manutencao-{m.Id}",
                m.ImovelId,
                m.Imovel == null ? null : m.Imovel.Nome,
                "manutencao",
                string.IsNullOrWhiteSpace(m.Categoria) ? "Manutenção" : m.Categoria,
                m.DataPrevista ?? m.DataAbertura,
                (m.DataPrevista ?? m.DataAbertura).AddDays(1),
                m.Status.ToString()))
            .ToListAsync(cancellationToken);

        var calendario = reservas
            .Select(r => new PortalCalendarioEventoResponse(
                $"reserva-{r.Id}",
                r.ImovelId,
                r.ImovelNome,
                "reserva",
                r.ImovelNome,
                r.CheckIn,
                r.CheckOut,
                r.Status))
            .Concat(repasses.Select(r => new PortalCalendarioEventoResponse(
                $"repasse-{r.Id}",
                r.ImovelId,
                r.ImovelNome,
                "repasse",
                "Repasse",
                r.PeriodoFim,
                r.PeriodoFim.AddDays(1),
                r.Status)))
            .Concat(bloqueios)
            .Concat(manutencoes)
            .OrderBy(e => e.Inicio)
            .ToList();

        var receitas = movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita.ToString())
            .Sum(m => m.Valor);

        var custos = movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa.ToString())
            .Sum(m => m.Valor);

        var resumoPorImovel = imoveis
            .Where(i => selectedImovelIds.Contains(i.Id))
            .Select(i =>
            {
                var reservasImovel = reservas.Where(r => r.ImovelId == i.Id).ToArray();
                var movimentacoesImovel = movimentacoes.Where(m => m.ImovelId == i.Id).ToArray();
                var repassesImovel = repasses.Where(r => r.ImovelId == i.Id).ToArray();
                var receitaImovel = reservasImovel.Sum(r => r.Receita) +
                    movimentacoesImovel
                        .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita.ToString())
                        .Sum(m => m.Valor);
                var custosImovel = movimentacoesImovel
                    .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa.ToString())
                    .Sum(m => m.Valor);

                return new PortalImovelResumoResponse(
                    i.Id,
                    i.Nome,
                    i.FotoPrincipal,
                    receitaImovel,
                    custosImovel,
                    receitaImovel - custosImovel,
                    reservasImovel.Length,
                    repassesImovel.Sum(r => r.SaldoPendente),
                    i.PercentualSocio);
            })
            .OrderByDescending(i => i.Lucro)
            .ToList();

        var visaoCalculo = PortalSocioInsightBuilder.Build(imoveis, reservas, movimentacoes, repasses);

        return PortalDataResult.Success(new PortalProprietarioResponse(
            proprietario.Id,
            proprietario.Nome,
            start,
            end,
            imovelId,
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
            calendario,
            resumoPorImovel,
            visaoCalculo));
    }

    private IActionResult? ToErrorResult(PortalDataResult result)
    {
        if (result.IsForbidden)
        {
            return Forbid();
        }

        return string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? null
            : BadRequest(new { message = result.ErrorMessage });
    }

    private int? GetProprietarioId()
    {
        return int.TryParse(User.FindFirstValue("ProprietarioId"), out var proprietarioId)
            ? proprietarioId
            : null;
    }

    private static EnumFilterResult<TEnum> ParseEnumFilter<TEnum>(string? value, string label)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new EnumFilterResult<TEnum>(null, null);
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? new EnumFilterResult<TEnum>(parsed, null)
            : new EnumFilterResult<TEnum>(null, $"Filtro de {label} inválido.");
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static byte[] BuildDemonstrativoRepassePdf(Domain.Entities.RepasseProprietario repasse)
    {
        var propertyName = repasse.Imovel?.Nome ?? "Todos os imoveis";
        var baseCalculo = repasse.ReceitaReservas - repasse.TaxasPlataforma - repasse.CustosVinculados - repasse.ComissaoAdministradora;
        var summary = new List<SimplePdfSummaryItem>
        {
            new("Sócio", repasse.Proprietario?.Nome ?? "-"),
            new("Imóvel", propertyName),
            new("Período", $"{FormatDate(repasse.PeriodoInicio)} a {FormatDate(repasse.PeriodoFim)}"),
            new("Status", repasse.Status.ToString()),
            new("Receitas de reservas", FormatCurrency(repasse.ReceitaReservas)),
            new("Taxas da plataforma", FormatCurrency(repasse.TaxasPlataforma)),
            new("Custos vinculados", FormatCurrency(repasse.CustosVinculados)),
            new("Comissão administradora", FormatCurrency(repasse.ComissaoAdministradora)),
            new("Base do cálculo", FormatCurrency(baseCalculo)),
            new("Percentual do sócio", $"{repasse.PercentualSocio:0.##}%"),
            new("Valor a repassar", FormatCurrency(repasse.ValorRepassar)),
            new("Valor pago", FormatCurrency(repasse.ValorPago)),
            new("Saldo pendente", FormatCurrency(repasse.ValorRepassar - repasse.ValorPago))
        };

        var rows = repasse.Itens
            .OrderBy(i => i.Id)
            .Select(item => (IReadOnlyCollection<string>)
            [
                item.Descricao,
                FormatCurrency(item.Receita),
                FormatCurrency(item.Taxas),
                FormatCurrency(item.Custos),
                FormatCurrency(item.Comissao),
                FormatCurrency(item.ValorLiquido)
            ])
            .ToList();

        return SimplePdfBuilder.CreateReport(new SimplePdfReport(
            "Demonstrativo de Repasse",
            $"Repasse #{repasse.Id} - {propertyName}",
            summary,
            [
                new SimplePdfTable(
                    "Memória de cálculo",
                    ["Descricao", "Receita", "Taxas", "Custos", "Comissao", "Liquido"],
                    rows)
            ],
            [
                "Documento gerado automaticamente pelo RentalHub.",
                "Os valores devem ser conferidos conforme contrato de administração do imóvel.",
                $"Percentual aplicado neste fechamento: {repasse.PercentualSocio:0.##}%.",
                $"Emitido em {FormatDate(DateTime.UtcNow)}."
            ]));
    }

    private static byte[] BuildReservasPdf(PortalProprietarioResponse data)
    {
        var rows = data.Reservas
            .OrderBy(r => r.CheckIn)
            .Select(r => (IReadOnlyCollection<string>)
            [
                FormatDate(r.CheckIn),
                FormatDate(r.CheckOut),
                r.ImovelNome,
                r.HospedeNome,
                r.Origem,
                FormatCurrency(r.Receita),
                FormatCurrency(r.ValorLiquido),
                r.Status
            ])
            .ToList();

        return SimplePdfBuilder.CreateReport(new SimplePdfReport(
            "Reservas do Sócio",
            BuildPortalSubtitle(data),
            BuildPortalSummary(data),
            [
                new SimplePdfTable(
                    "Reservas",
                    ["Check-in", "Check-out", "Imovel", "Hospede", "Origem", "Receita", "Liquido", "Status"],
                    rows)
            ],
            ["Relatório gerado pelo Portal do Sócio."]));
    }

    private static byte[] BuildMovimentacoesPdf(PortalProprietarioResponse data)
    {
        var receitas = data.Movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita.ToString())
            .Sum(m => m.Valor);
        var despesas = data.Movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa.ToString())
            .Sum(m => m.Valor);
        var rows = data.Movimentacoes
            .OrderByDescending(m => m.Data)
            .Select(m => (IReadOnlyCollection<string>)
            [
                FormatDate(m.Data),
                m.Tipo,
                m.CategoriaNome,
                m.ImovelNome ?? "-",
                m.Descricao,
                FormatCurrency(m.Valor)
            ])
            .ToList();

        var summary = BuildPortalSummary(data)
            .Concat([
                new SimplePdfSummaryItem("Entradas", FormatCurrency(receitas)),
                new SimplePdfSummaryItem("Saidas", FormatCurrency(despesas)),
                new SimplePdfSummaryItem("Saldo", FormatCurrency(receitas - despesas))
            ])
            .ToList();

        return SimplePdfBuilder.CreateReport(new SimplePdfReport(
            "Receitas e Custos do Sócio",
            BuildPortalSubtitle(data),
            summary,
            [
                new SimplePdfTable(
                    "Movimentacoes",
                    ["Data", "Tipo", "Categoria", "Imovel", "Descricao", "Valor"],
                    rows)
            ],
            ["Relatório gerado pelo Portal do Sócio."]));
    }

    private static byte[] BuildRepassesPdf(PortalProprietarioResponse data)
    {
        var rows = data.Repasses
            .OrderByDescending(r => r.PeriodoFim)
            .Select(r => (IReadOnlyCollection<string>)
            [
                $"{FormatDate(r.PeriodoInicio)} a {FormatDate(r.PeriodoFim)}",
                r.ImovelNome ?? "Todos",
                $"{r.PercentualSocio:0.##}%",
                FormatCurrency(r.ValorRepassar),
                FormatCurrency(r.ValorPago),
                FormatCurrency(r.SaldoPendente),
                r.Status
            ])
            .ToList();

        var summary = BuildPortalSummary(data)
            .Concat([
                new SimplePdfSummaryItem("Repasses gerados", FormatCurrency(data.RepassesGerados)),
                new SimplePdfSummaryItem("Repasses pendentes", FormatCurrency(data.RepassesPendentes))
            ])
            .ToList();

        return SimplePdfBuilder.CreateReport(new SimplePdfReport(
            "Repasses do Sócio",
            BuildPortalSubtitle(data),
            summary,
            [
                new SimplePdfTable(
                    "Repasses",
                    ["Periodo", "Imovel", "% Sócio", "Valor", "Pago", "Pendente", "Status"],
                    rows)
            ],
            ["Relatório gerado pelo Portal do Sócio."]));
    }

    private static byte[] BuildDemonstrativoMensalPdf(PortalProprietarioResponse data)
    {
        var visao = data.VisaoCalculo;
        var liquidoReservas = data.Reservas.Sum(r => r.ValorLiquido);
        var executiveSummary = new List<SimplePdfSummaryItem>
        {
            new("Sócio", data.ProprietarioNome),
            new("Período", $"{FormatDate(data.PeriodoInicio)} a {FormatDate(data.PeriodoFim)}"),
            new("Imóveis analisados", data.ResumoPorImovel.Count.ToString()),
            new("Reservas no período", data.Reservas.Count.ToString()),
            new("Receita bruta", FormatCurrency(visao.ReceitaReservas)),
            new("Líquido das reservas", FormatCurrency(liquidoReservas)),
            new("Base operacional", FormatCurrency(visao.ResultadoOperacional)),
            new("Repasse oficial gerado", FormatCurrency(visao.RepassesGerados)),
            new("Saldo pendente", FormatCurrency(visao.RepassesPendentes))
        };

        var compositionSummary = new List<SimplePdfSummaryItem>
        {
            new("Receitas extras", FormatCurrency(visao.ReceitasExtras)),
            new("Custos vinculados", FormatCurrency(visao.CustosVinculados)),
            new("Custos sem vínculo", FormatCurrency(visao.CustosSemVinculo)),
            new("Imóveis sem repasse", visao.ImoveisSemRepasseNoPeriodo.ToString()),
            new("Percentuais a conferir", visao.ImoveisComPercentualDivergente.ToString())
        };

        var propertyRows = visao.MemoriaCalculo
            .OrderByDescending(i => i.ResultadoOperacional)
            .Select(i => (IReadOnlyCollection<string>)
            [
                i.ImovelNome,
                $"{i.PercentualSocioAtual:0.##}%",
                FormatCurrency(i.ReceitaReservas),
                FormatCurrency(i.ReceitasExtras),
                FormatCurrency(i.Custos),
                FormatCurrency(i.ResultadoOperacional),
                FormatCurrency(i.RepassesGerados),
                FormatCurrency(i.RepassesPendentes)
            ])
            .ToList();

        var reservationRows = data.Reservas
            .OrderBy(r => r.CheckIn)
            .Select(r => (IReadOnlyCollection<string>)
            [
                $"{FormatDate(r.CheckIn)} a {FormatDate(r.CheckOut)}",
                r.ImovelNome,
                r.HospedeNome,
                r.Origem,
                FormatCurrency(r.Receita),
                FormatCurrency(r.ValorLiquido)
            ])
            .ToList();

        var transferRows = data.Repasses
            .OrderByDescending(r => r.PeriodoFim)
            .Select(r => (IReadOnlyCollection<string>)
            [
                $"{FormatDate(r.PeriodoInicio)} a {FormatDate(r.PeriodoFim)}",
                r.ImovelNome ?? "Todos",
                $"{r.PercentualSocio:0.##}%",
                FormatCurrency(r.ValorRepassar),
                FormatCurrency(r.ValorPago),
                FormatCurrency(r.SaldoPendente),
                r.Status
            ])
            .ToList();

        var lines = new List<string>
        {
            "RentalHub",
            "# Demonstrativo mensal do sócio",
            $"> {data.ProprietarioNome} · {FormatDate(data.PeriodoInicio)} a {FormatDate(data.PeriodoFim)}",
            $"> Emitido em {FormatDate(DateTime.UtcNow)}",
            string.Empty,
            "## Visão executiva",
        };

        lines.AddRange(SimplePdfBuilder.RenderSummary(executiveSummary));
        lines.Add(string.Empty);
        lines.Add("## Composição do período");
        lines.AddRange(SimplePdfBuilder.RenderSummary(compositionSummary));
        lines.Add(string.Empty);
        lines.Add("## Leitura por imóvel");
        lines.Add("> Esta seção ajuda a entender formação do resultado e o que já virou repasse oficial.");
        lines.AddRange(SimplePdfBuilder.RenderTable(new SimplePdfTable(
            "Memória de cálculo por imóvel",
            ["Imovel", "% Sócio", "Reservas", "Receitas extras", "Custos", "Base operacional", "Repasse gerado", "A receber"],
            propertyRows)));
        lines.Add(string.Empty);
        lines.Add("## Repasses emitidos");
        lines.AddRange(SimplePdfBuilder.RenderTable(new SimplePdfTable(
            "Repasses",
            ["Periodo", "Imovel", "% Sócio", "Valor", "Pago", "Pendente", "Status"],
            transferRows)));
        lines.Add(string.Empty);
        lines.Add("## Reservas do período");
        lines.AddRange(SimplePdfBuilder.RenderTable(new SimplePdfTable(
            "Reservas do período",
            ["Periodo", "Imovel", "Hospede", "Origem", "Bruto", "Liquido"],
            reservationRows)));
        lines.Add(string.Empty);
        lines.Add("## Pontos de atenção");
        lines.AddRange(visao.Alertas.Select(alerta => $"- {alerta}"));
        lines.Add("- Custos sem vínculo não entram no repasse até serem associados a um imóvel ou reserva.");
        lines.Add("- Quando houver mudança de percentual no tempo, o repasse oficial prevalece sobre a leitura resumida do portal.");
        lines.Add(string.Empty);
        lines.Add("## Leitura rápida");
        lines.Add($"> Base operacional do período: {FormatCurrency(visao.ResultadoOperacional)}");
        lines.Add($"> Repasse oficial gerado: {FormatCurrency(visao.RepassesGerados)}");
        lines.Add($"> Saldo pendente ao sócio: {FormatCurrency(visao.RepassesPendentes)}");

        return SimplePdfBuilder.Create(lines);
    }

    private static byte[] BuildReservaDetalhePdf(Domain.Entities.Reserva reserva)
    {
        var nights = Math.Max(1, (int)(reserva.CheckOut.Date - reserva.CheckIn.Date).TotalDays);
        var receitaBruta = reserva.ValorHospedagem + reserva.TaxaLimpeza;
        var summary = new List<SimplePdfSummaryItem>
        {
            new("Reserva", $"#{reserva.Id}"),
            new("Imóvel", reserva.Imovel?.Nome ?? "-"),
            new("Hóspede", reserva.Hospede?.Nome ?? "-"),
            new("Origem", reserva.Origem.ToString()),
            new("Status", reserva.Status.ToString()),
            new("Check-in", FormatDate(reserva.CheckIn)),
            new("Check-out", FormatDate(reserva.CheckOut)),
            new("Diárias", nights.ToString()),
            new("Hóspedes", reserva.NumeroHospedes.ToString()),
            new("Bruto por diária", FormatCurrency(receitaBruta / nights)),
            new("Líquido por diária", FormatCurrency(reserva.ValorLiquido / nights)),
            new("Receita bruta", FormatCurrency(receitaBruta)),
            new("Taxa da plataforma", FormatCurrency(reserva.TaxaPlataforma)),
            new("Comissão administradora", FormatCurrency(reserva.ComissaoAdministradora)),
            new("Valor líquido", FormatCurrency(reserva.ValorLiquido))
        };

        return SimplePdfBuilder.CreateReport(new SimplePdfReport(
            "Detalhe da Reserva",
            $"{reserva.Imovel?.Nome ?? "Imóvel"} - {FormatDate(reserva.CheckIn)} a {FormatDate(reserva.CheckOut)}",
            summary,
            [],
            [
                string.IsNullOrWhiteSpace(reserva.Observacoes) ? "Sem observações registradas." : $"Observações: {reserva.Observacoes}",
                "Documento gerado automaticamente pelo RentalHub."
            ]));
    }

    private static IReadOnlyCollection<SimplePdfSummaryItem> BuildPortalSummary(PortalProprietarioResponse data)
    {
        return
        [
            new("Sócio", data.ProprietarioNome),
            new("Período", $"{FormatDate(data.PeriodoInicio)} a {FormatDate(data.PeriodoFim)}"),
            new("Imóveis", data.TotalImoveis.ToString()),
            new("Reservas", data.TotalReservas.ToString())
        ];
    }

    private static string BuildPortalSubtitle(PortalProprietarioResponse data)
    {
        return $"Período {FormatDate(data.PeriodoInicio)} a {FormatDate(data.PeriodoFim)}";
    }

    private static string FormatDate(DateTime value) => value.ToString("dd/MM/yyyy");

    private static string FormatCurrency(decimal value) => $"R$ {value:0.00}";

    private static string FormatDateForFile(DateTime value) => value.ToString("yyyy-MM-dd");
}

internal sealed record PortalDataResult(
    PortalProprietarioResponse? Data,
    string? ErrorMessage,
    bool IsForbidden)
{
    public static PortalDataResult Success(PortalProprietarioResponse data) => new(data, null, false);

    public static PortalDataResult Invalid(string message) => new(null, message, false);

    public static PortalDataResult Forbidden() => new(null, null, true);
}

internal sealed record EnumFilterResult<TEnum>(TEnum? Value, string? ErrorMessage)
    where TEnum : struct, Enum;

public sealed record PortalProprietarioResponse(
    int ProprietarioId,
    string ProprietarioNome,
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    int? ImovelSelecionadoId,
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
    IReadOnlyCollection<PortalCalendarioEventoResponse> Calendario,
    IReadOnlyCollection<PortalImovelResumoResponse> ResumoPorImovel,
    PortalVisaoCalculoResponse VisaoCalculo);

public sealed record PortalImovelResponse(
    int Id,
    string Nome,
    string CodigoInterno,
    string? Cidade,
    string? Estado,
    string Status,
    int QuantidadeHospedes,
    int QuantidadeQuartos,
    int QuantidadeBanheiros,
    decimal PercentualSocio,
    string? FotoPrincipal);

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
    int? ImovelId,
    DateTime Data,
    string Tipo,
    string CategoriaNome,
    string? ImovelNome,
    string Descricao,
    decimal Valor);

public sealed record PortalRepasseResponse(
    int Id,
    int? ImovelId,
    string? ImovelNome,
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    decimal PercentualSocio,
    decimal ValorRepassar,
    decimal ValorPago,
    decimal SaldoPendente,
    string Status);

public sealed record PortalRepasseDetalheResponse(
    int Id,
    int? ImovelId,
    string? ImovelNome,
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    decimal ReceitaReservas,
    decimal TaxasPlataforma,
    decimal CustosVinculados,
    decimal ComissaoAdministradora,
    decimal PercentualSocio,
    decimal ValorRepassar,
    decimal ValorPago,
    decimal SaldoPendente,
    string Status,
    DateTime? DataPagamento,
    string? Observacoes,
    IReadOnlyCollection<PortalRepasseItemResponse> Itens);

public sealed record PortalRepasseItemResponse(
    int Id,
    int? ReservaId,
    int? MovimentacaoFinanceiraId,
    string Descricao,
    decimal Receita,
    decimal Taxas,
    decimal Custos,
    decimal Comissao,
    decimal ValorLiquido);

public sealed record PortalCalendarioEventoResponse(
    string Id,
    int? ImovelId,
    string? ImovelNome,
    string Tipo,
    string Titulo,
    DateTime Inicio,
    DateTime Fim,
    string Status);

public sealed record PortalImovelResumoResponse(
    int ImovelId,
    string ImovelNome,
    string? FotoPrincipal,
    decimal Receitas,
    decimal Custos,
    decimal Lucro,
    int Reservas,
    decimal RepassesPendentes,
    decimal PercentualSocio);

public sealed record PortalVisaoCalculoResponse(
    decimal ReceitaReservas,
    decimal ReceitasExtras,
    decimal CustosVinculados,
    decimal CustosSemVinculo,
    decimal ResultadoOperacional,
    decimal RepassesGerados,
    decimal RepassesPendentes,
    int CustosSemVinculoQuantidade,
    int ImoveisSemRepasseNoPeriodo,
    int ImoveisComPercentualDivergente,
    IReadOnlyCollection<PortalMemoriaCalculoItemResponse> MemoriaCalculo,
    IReadOnlyCollection<string> Alertas);

public sealed record PortalMemoriaCalculoItemResponse(
    int ImovelId,
    string ImovelNome,
    decimal PercentualSocioAtual,
    decimal ReceitaReservas,
    decimal ReceitasExtras,
    decimal Custos,
    decimal ResultadoOperacional,
    decimal RepassesGerados,
    decimal RepassesPendentes,
    bool TemRepasseNoPeriodo,
    bool PercentualDivergenteNoPeriodo);
