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
    public async Task<ActionResult<PortalProprietarioResponse>> GetPortal(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
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
                i.Status.ToString(),
                i.QuantidadeHospedes,
                i.QuantidadeQuartos,
                i.QuantidadeBanheiros,
                i.Fotos
                    .OrderByDescending(f => f.Principal)
                    .ThenBy(f => f.Ordem)
                    .Select(f => f.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var imovelIds = imoveis.Select(i => i.Id).ToArray();
        if (imovelId.HasValue && !imovelIds.Contains(imovelId.Value))
        {
            return BadRequest(new { message = "Imóvel não pertence ao proprietário autenticado." });
        }

        var selectedImovelIds = imovelId.HasValue ? [imovelId.Value] : imovelIds;

        var reservas = await _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .Where(r =>
                selectedImovelIds.Contains(r.ImovelId) &&
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
                    repassesImovel.Sum(r => r.SaldoPendente));
            })
            .OrderByDescending(i => i.Lucro)
            .ToList();

        return Ok(new PortalProprietarioResponse(
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
            resumoPorImovel));
    }

    [HttpGet("reservas.pdf")]
    public async Task<IActionResult> ReservasPdf(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        CancellationToken cancellationToken)
    {
        var result = await LoadPortalDataAsync(inicio, fim, imovelId, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var result = await LoadPortalDataAsync(inicio, fim, imovelId, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var result = await LoadPortalDataAsync(inicio, fim, imovelId, cancellationToken);
        var error = ToErrorResult(result);
        if (error is not null)
        {
            return error;
        }

        var data = result.Data!;
        var pdf = BuildRepassesPdf(data);
        return File(pdf, "application/pdf", $"portal-repasses-{FormatDateForFile(data.PeriodoInicio)}-{FormatDateForFile(data.PeriodoFim)}.pdf");
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
                i.Fotos
                    .OrderByDescending(f => f.Principal)
                    .ThenBy(f => f.Ordem)
                    .Select(f => f.Url)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var imovelIds = imoveis.Select(i => i.Id).ToArray();
        if (imovelId.HasValue && !imovelIds.Contains(imovelId.Value))
        {
            return PortalDataResult.Invalid("Imóvel não pertence ao proprietário autenticado.");
        }

        var selectedImovelIds = imovelId.HasValue ? [imovelId.Value] : imovelIds;

        var reservas = await _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .Where(r =>
                selectedImovelIds.Contains(r.ImovelId) &&
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
                r.ValorRepassar,
                r.ValorPago,
                r.ValorRepassar - r.ValorPago,
                r.Status.ToString()))
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
                    repassesImovel.Sum(r => r.SaldoPendente));
            })
            .OrderByDescending(i => i.Lucro)
            .ToList();

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
            resumoPorImovel));
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

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static byte[] BuildDemonstrativoRepassePdf(Domain.Entities.RepasseProprietario repasse)
    {
        var propertyName = repasse.Imovel?.Nome ?? "Todos os imoveis";
        var summary = new List<SimplePdfSummaryItem>
        {
            new("Proprietario", repasse.Proprietario?.Nome ?? "-"),
            new("Imovel", propertyName),
            new("Periodo", $"{FormatDate(repasse.PeriodoInicio)} a {FormatDate(repasse.PeriodoFim)}"),
            new("Status", repasse.Status.ToString()),
            new("Receitas de reservas", FormatCurrency(repasse.ReceitaReservas)),
            new("Taxas da plataforma", FormatCurrency(repasse.TaxasPlataforma)),
            new("Custos vinculados", FormatCurrency(repasse.CustosVinculados)),
            new("Comissao administradora", FormatCurrency(repasse.ComissaoAdministradora)),
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
                    "Itens do demonstrativo",
                    ["Descricao", "Receita", "Taxas", "Custos", "Comissao", "Liquido"],
                    rows)
            ],
            [
                "Documento gerado automaticamente pelo RentalHub.",
                "Os valores devem ser conferidos conforme contrato de administracao do imovel.",
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
            "Reservas do Proprietario",
            BuildPortalSubtitle(data),
            BuildPortalSummary(data),
            [
                new SimplePdfTable(
                    "Reservas",
                    ["Check-in", "Check-out", "Imovel", "Hospede", "Origem", "Receita", "Liquido", "Status"],
                    rows)
            ],
            ["Relatorio gerado pelo Portal do Proprietario."]));
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
            "Receitas e Custos do Proprietario",
            BuildPortalSubtitle(data),
            summary,
            [
                new SimplePdfTable(
                    "Movimentacoes",
                    ["Data", "Tipo", "Categoria", "Imovel", "Descricao", "Valor"],
                    rows)
            ],
            ["Relatorio gerado pelo Portal do Proprietario."]));
    }

    private static byte[] BuildRepassesPdf(PortalProprietarioResponse data)
    {
        var rows = data.Repasses
            .OrderByDescending(r => r.PeriodoFim)
            .Select(r => (IReadOnlyCollection<string>)
            [
                $"{FormatDate(r.PeriodoInicio)} a {FormatDate(r.PeriodoFim)}",
                r.ImovelNome ?? "Todos",
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
            "Repasses do Proprietario",
            BuildPortalSubtitle(data),
            summary,
            [
                new SimplePdfTable(
                    "Repasses",
                    ["Periodo", "Imovel", "Valor", "Pago", "Pendente", "Status"],
                    rows)
            ],
            ["Relatorio gerado pelo Portal do Proprietario."]));
    }

    private static IReadOnlyCollection<SimplePdfSummaryItem> BuildPortalSummary(PortalProprietarioResponse data)
    {
        return
        [
            new("Proprietario", data.ProprietarioNome),
            new("Periodo", $"{FormatDate(data.PeriodoInicio)} a {FormatDate(data.PeriodoFim)}"),
            new("Imoveis", data.TotalImoveis.ToString()),
            new("Reservas", data.TotalReservas.ToString())
        ];
    }

    private static string BuildPortalSubtitle(PortalProprietarioResponse data)
    {
        return $"Periodo {FormatDate(data.PeriodoInicio)} a {FormatDate(data.PeriodoFim)}";
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
    IReadOnlyCollection<PortalImovelResumoResponse> ResumoPorImovel);

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
    decimal ValorRepassar,
    decimal ValorPago,
    decimal SaldoPendente,
    string Status);

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
    decimal RepassesPendentes);
