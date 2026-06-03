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
        var lines = new List<string>
        {
            "RentalHub - Demonstrativo de Repasse",
            $"Repasse #{repasse.Id}",
            $"Proprietario: {repasse.Proprietario?.Nome ?? string.Empty}",
            $"Imovel: {repasse.Imovel?.Nome ?? "Todos os imoveis"}",
            $"Periodo: {FormatDate(repasse.PeriodoInicio)} a {FormatDate(repasse.PeriodoFim)}",
            $"Status: {repasse.Status}",
            string.Empty,
            $"Receitas: {FormatCurrency(repasse.ReceitaReservas)}",
            $"Taxas da plataforma: {FormatCurrency(repasse.TaxasPlataforma)}",
            $"Custos vinculados: {FormatCurrency(repasse.CustosVinculados)}",
            $"Comissao da administradora: {FormatCurrency(repasse.ComissaoAdministradora)}",
            $"Valor a repassar: {FormatCurrency(repasse.ValorRepassar)}",
            $"Valor pago: {FormatCurrency(repasse.ValorPago)}",
            $"Saldo pendente: {FormatCurrency(repasse.ValorRepassar - repasse.ValorPago)}",
            string.Empty,
            "Itens",
            "Descricao | Receita | Taxas | Custos | Comissao | Liquido"
        };

        lines.AddRange(repasse.Itens
            .OrderBy(i => i.Id)
            .Select(item =>
                $"{item.Descricao} | {FormatCurrency(item.Receita)} | {FormatCurrency(item.Taxas)} | {FormatCurrency(item.Custos)} | {FormatCurrency(item.Comissao)} | {FormatCurrency(item.ValorLiquido)}"));

        lines.Add(string.Empty);
        lines.Add($"Emitido em {FormatDate(DateTime.UtcNow)}");

        return SimplePdfBuilder.Create(lines);
    }

    private static string FormatDate(DateTime value) => value.ToString("dd/MM/yyyy");

    private static string FormatCurrency(decimal value) => $"R$ {value:0.00}";
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
