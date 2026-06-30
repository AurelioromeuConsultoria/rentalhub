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
public sealed class RepassesController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public RepassesController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<RepasseProprietarioResponse>>> GetAll(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? proprietarioId,
        [FromQuery] int? imovelId,
        [FromQuery] RepasseStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = BaseQuery();

        if (inicio.HasValue)
        {
            query = query.Where(r => r.PeriodoFim >= NormalizeDate(inicio.Value));
        }

        if (fim.HasValue)
        {
            query = query.Where(r => r.PeriodoInicio <= NormalizeDate(fim.Value));
        }

        if (proprietarioId.HasValue)
        {
            query = query.Where(r => r.ProprietarioId == proprietarioId.Value);
        }

        if (imovelId.HasValue)
        {
            query = query.Where(r => r.ImovelId == imovelId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.PeriodoFim)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => ToResponse(r))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<RepasseProprietarioResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RepasseProprietarioResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var repasse = await BaseQuery().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return repasse is null ? NotFound() : Ok(ToResponse(repasse));
    }

    [HttpPost("gerar")]
    public async Task<ActionResult<RepasseProprietarioResponse>> Gerar(
        GerarRepasseRequest request,
        CancellationToken cancellationToken)
    {
        var inicio = NormalizeDate(request.PeriodoInicio);
        var fim = NormalizeDate(request.PeriodoFim);

        if (fim < inicio)
        {
            return BadRequest(new { message = "Período final deve ser maior ou igual ao período inicial." });
        }

        var socio = await _dbContext.Proprietarios
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProprietarioId && p.Ativo, cancellationToken);

        if (socio is null)
        {
            return BadRequest(new { message = "Sócio ativo não encontrado." });
        }

        if (!request.ImovelId.HasValue)
        {
            return BadRequest(new { message = "Selecione o imóvel para calcular o percentual de repasse do sócio." });
        }

        var imovel = await _dbContext.Imoveis
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.Id == request.ImovelId.Value && i.ProprietarioId == request.ProprietarioId,
                cancellationToken);

        if (imovel is null)
        {
            return BadRequest(new { message = "Imóvel não encontrado para este sócio." });
        }

        var alreadyExists = await _dbContext.RepassesProprietarios.AnyAsync(
            r => r.ProprietarioId == request.ProprietarioId &&
                 r.PeriodoInicio == inicio &&
                 r.PeriodoFim == fim &&
                 r.ImovelId == request.ImovelId,
            cancellationToken);

        if (alreadyExists)
        {
            return Conflict(new { message = "Já existe repasse gerado para este sócio, imóvel e período." });
        }

        var reservasQuery = _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Where(r =>
                r.Status != ReservaStatus.Cancelada &&
                r.CheckOut >= inicio &&
                r.CheckOut <= fim &&
                r.Imovel != null &&
                r.Imovel.ProprietarioId == request.ProprietarioId &&
                r.ImovelId == request.ImovelId.Value);

        var reservas = await reservasQuery
            .OrderBy(r => r.CheckOut)
            .ToListAsync(cancellationToken);

        var reservaIds = reservas.Select(r => r.Id).ToArray();
        var custosPorReserva = reservaIds.Length == 0
            ? new Dictionary<int, decimal>()
            : await _dbContext.MovimentacoesFinanceiras
                .AsNoTracking()
                .Where(m =>
                    m.Tipo == MovimentacaoFinanceiraTipo.Despesa &&
                    m.ReservaId.HasValue &&
                    reservaIds.Contains(m.ReservaId.Value))
                .GroupBy(m => m.ReservaId!.Value)
                .Select(g => new { ReservaId = g.Key, Total = g.Sum(m => m.Valor) })
                .ToDictionaryAsync(g => g.ReservaId, g => g.Total, cancellationToken);

        var custosOperacionaisQuery = _dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Include(m => m.Imovel)
            .Where(m =>
                m.Tipo == MovimentacaoFinanceiraTipo.Despesa &&
                m.ReservaId == null &&
                m.Data >= inicio &&
                m.Data <= fim &&
                m.ImovelId == request.ImovelId.Value);

        custosOperacionaisQuery = custosOperacionaisQuery.Where(m => m.ImovelId == request.ImovelId.Value);

        var custosOperacionais = await custosOperacionaisQuery
            .OrderBy(m => m.Data)
            .ToListAsync(cancellationToken);

        var itens = new List<RepasseItem>();
        var percentualSocio = imovel.PercentualRepasseSocio ?? 100;
        foreach (var reserva in reservas)
        {
            var receita = reserva.ValorHospedagem + reserva.TaxaLimpeza;
            var custos = custosPorReserva.TryGetValue(reserva.Id, out var totalCustos) ? totalCustos : 0;
            var lucroLiquido = receita - reserva.TaxaPlataforma - custos - reserva.ComissaoAdministradora;
            var valorSocio = CalculateSocioShare(lucroLiquido, percentualSocio);

            itens.Add(new RepasseItem
            {
                TenantId = _dbContext.CurrentTenantId,
                ReservaId = reserva.Id,
                Descricao = $"Reserva #{reserva.Id} - {reserva.Imovel?.Nome ?? "Imóvel"} ({percentualSocio:0.##}% do lucro)",
                Receita = receita,
                Taxas = reserva.TaxaPlataforma,
                Custos = custos,
                Comissao = reserva.ComissaoAdministradora,
                ValorLiquido = valorSocio
            });
        }

        foreach (var custo in custosOperacionais)
        {
            var valorSocio = CalculateSocioShare(-custo.Valor, percentualSocio);

            itens.Add(new RepasseItem
            {
                TenantId = _dbContext.CurrentTenantId,
                MovimentacaoFinanceiraId = custo.Id,
                Descricao = $"Custo operacional - {custo.Descricao} ({percentualSocio:0.##}% do lucro)",
                Receita = 0,
                Taxas = 0,
                Custos = custo.Valor,
                Comissao = 0,
                ValorLiquido = valorSocio
            });
        }

        if (itens.Count == 0)
        {
            return BadRequest(new { message = "Não há reservas ou custos no período informado para gerar repasse." });
        }

        var repasse = new RepasseProprietario
        {
            TenantId = _dbContext.CurrentTenantId,
            ProprietarioId = request.ProprietarioId,
            ImovelId = request.ImovelId,
            PeriodoInicio = inicio,
            PeriodoFim = fim,
            ReceitaReservas = itens.Sum(i => i.Receita),
            TaxasPlataforma = itens.Sum(i => i.Taxas),
            CustosVinculados = itens.Sum(i => i.Custos),
            ComissaoAdministradora = itens.Sum(i => i.Comissao),
            PercentualSocio = percentualSocio,
            ValorRepassar = itens.Sum(i => i.ValorLiquido),
            ValorPago = 0,
            Status = RepasseStatus.Pendente,
            Observacoes = request.Observacoes?.Trim(),
            DataCriacao = DateTime.UtcNow,
            Itens = itens
        };

        _dbContext.RepassesProprietarios.Add(repasse);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await BaseQuery().FirstAsync(r => r.Id == repasse.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = repasse.Id }, ToResponse(created));
    }

    [HttpPost("{id:int}/pagamentos")]
    public async Task<ActionResult<RepasseProprietarioResponse>> RegistrarPagamento(
        int id,
        RegistrarPagamentoRepasseRequest request,
        CancellationToken cancellationToken)
    {
        var repasse = await _dbContext.RepassesProprietarios.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (repasse is null)
        {
            return NotFound();
        }

        if (request.Valor <= 0)
        {
            return BadRequest(new { message = "Valor do pagamento deve ser maior que zero." });
        }

        var saldoPendente = repasse.ValorRepassar - repasse.ValorPago;
        if (request.Valor > saldoPendente)
        {
            return BadRequest(new { message = "Valor do pagamento excede o saldo pendente do repasse." });
        }

        repasse.ValorPago += request.Valor;
        repasse.DataPagamento = NormalizeDate(request.DataPagamento ?? DateTime.UtcNow);
        repasse.Observacoes = string.IsNullOrWhiteSpace(request.Observacoes)
            ? repasse.Observacoes
            : request.Observacoes.Trim();
        repasse.DataAtualizacao = DateTime.UtcNow;
        repasse.Status = repasse.ValorPago >= repasse.ValorRepassar
            ? RepasseStatus.Pago
            : RepasseStatus.ParcialmentePago;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await BaseQuery().FirstAsync(r => r.Id == id, cancellationToken);
        return Ok(ToResponse(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var repasse = await _dbContext.RepassesProprietarios.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (repasse is null)
        {
            return NotFound();
        }

        if (repasse.ValorPago > 0)
        {
            return BadRequest(new { message = "Repasses com pagamento registrado não podem ser excluídos." });
        }

        _dbContext.RepassesProprietarios.Remove(repasse);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private IQueryable<RepasseProprietario> BaseQuery()
    {
        return _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Include(r => r.Proprietario)
            .Include(r => r.Imovel)
            .Include(r => r.Itens)
                .ThenInclude(i => i.Reserva)
            .Include(r => r.Itens)
                .ThenInclude(i => i.MovimentacaoFinanceira);
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static decimal CalculateSocioShare(decimal lucroLiquido, decimal percentualSocio)
    {
        return Math.Round(lucroLiquido * percentualSocio / 100, 2, MidpointRounding.AwayFromZero);
    }

    private static RepasseProprietarioResponse ToResponse(RepasseProprietario repasse)
    {
        return new RepasseProprietarioResponse(
            repasse.Id,
            repasse.ProprietarioId,
            repasse.Proprietario?.Nome ?? string.Empty,
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
            repasse.Status,
            repasse.DataPagamento,
            repasse.Observacoes,
            repasse.DataCriacao,
            repasse.DataAtualizacao,
            repasse.Itens
                .OrderBy(i => i.ReservaId.HasValue ? 0 : 1)
                .ThenBy(i => i.Id)
                .Select(ToItemResponse)
                .ToList());
    }

    private static RepasseItemResponse ToItemResponse(RepasseItem item)
    {
        return new RepasseItemResponse(
            item.Id,
            item.ReservaId,
            item.MovimentacaoFinanceiraId,
            item.Descricao,
            item.Receita,
            item.Taxas,
            item.Custos,
            item.Comissao,
            item.ValorLiquido);
    }
}

public sealed record GerarRepasseRequest(
    int ProprietarioId,
    int? ImovelId,
    DateTime PeriodoInicio,
    DateTime PeriodoFim,
    string? Observacoes);

public sealed record RegistrarPagamentoRepasseRequest(
    decimal Valor,
    DateTime? DataPagamento,
    string? Observacoes);

public sealed record RepasseProprietarioResponse(
    int Id,
    int ProprietarioId,
    string ProprietarioNome,
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
    RepasseStatus Status,
    DateTime? DataPagamento,
    string? Observacoes,
    DateTime DataCriacao,
    DateTime? DataAtualizacao,
    IReadOnlyCollection<RepasseItemResponse> Itens);

public sealed record RepasseItemResponse(
    int Id,
    int? ReservaId,
    int? MovimentacaoFinanceiraId,
    string Descricao,
    decimal Receita,
    decimal Taxas,
    decimal Custos,
    decimal Comissao,
    decimal ValorLiquido);
