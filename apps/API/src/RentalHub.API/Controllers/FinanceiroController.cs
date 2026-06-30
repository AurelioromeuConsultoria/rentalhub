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
public sealed class FinanceiroController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public FinanceiroController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("movimentacoes")]
    public async Task<ActionResult<PagedResponse<MovimentacaoFinanceiraResponse>>> GetMovimentacoes(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] MovimentacaoFinanceiraTipo? tipo,
        [FromQuery] int? categoriaId,
        [FromQuery] int? imovelId,
        [FromQuery] int? proprietarioId,
        [FromQuery] int? reservaId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ApplyFilters(
            BaseQuery(),
            inicio,
            fim,
            tipo,
            categoriaId,
            imovelId,
            proprietarioId,
            reservaId);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.Data)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => ToResponse(m))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<MovimentacaoFinanceiraResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("movimentacoes/{id:int}")]
    public async Task<ActionResult<MovimentacaoFinanceiraResponse>> GetMovimentacaoById(int id, CancellationToken cancellationToken)
    {
        var movimentacao = await BaseQuery()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        return movimentacao is null ? NotFound() : Ok(ToResponse(movimentacao));
    }

    [HttpPost("movimentacoes")]
    public async Task<ActionResult<MovimentacaoFinanceiraResponse>> CreateMovimentacao(
        MovimentacaoFinanceiraRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var dataBase = NormalizeDate(request.Data);
        var ocorrencias = BuildOccurrenceDates(
            dataBase,
            request.Recorrente,
            request.RecorrenciaFrequencia,
            request.RecorrenciaIntervalo,
            request.RecorrenciaQuantidadeParcelas,
            request.RecorrenciaFim);

        var grupoRecorrenciaId = ocorrencias.Count > 1 ? Guid.NewGuid().ToString("N") : null;
        int? totalParcelas = ocorrencias.Count > 1 ? ocorrencias.Count : null;
        var movimentacoes = ocorrencias
            .Select((dataOcorrencia, index) => new MovimentacaoFinanceira
            {
                TenantId = _dbContext.CurrentTenantId,
                Tipo = request.Tipo,
                CategoriaFinanceiraId = request.CategoriaFinanceiraId,
                ImovelId = request.ImovelId,
                ReservaId = request.ReservaId,
                ProprietarioId = request.ProprietarioId,
                Data = dataOcorrencia,
                Descricao = request.Descricao.Trim(),
                Valor = request.Valor,
                Observacoes = request.Observacoes?.Trim(),
                GrupoRecorrenciaId = grupoRecorrenciaId,
                ParcelaAtual = totalParcelas.HasValue ? index + 1 : null,
                TotalParcelas = totalParcelas,
                RecorrenciaFrequencia = totalParcelas.HasValue ? request.RecorrenciaFrequencia : null,
                RecorrenciaIntervalo = totalParcelas.HasValue ? request.RecorrenciaIntervalo : null,
                RecorrenciaFim = totalParcelas.HasValue
                    ? (request.RecorrenciaFim.HasValue ? NormalizeDate(request.RecorrenciaFim.Value) : ocorrencias[^1])
                    : null,
                DataCriacao = DateTime.UtcNow
            })
            .ToList();

        _dbContext.MovimentacoesFinanceiras.AddRange(movimentacoes);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var movimentacao = await BaseQuery()
            .FirstAsync(m => m.Id == movimentacoes[0].Id, cancellationToken);

        return CreatedAtAction(nameof(GetMovimentacaoById), new { id = movimentacao.Id }, ToResponse(movimentacao));
    }

    [HttpPut("movimentacoes/{id:int}")]
    public async Task<ActionResult<MovimentacaoFinanceiraResponse>> UpdateMovimentacao(
        int id,
        MovimentacaoFinanceiraRequest request,
        CancellationToken cancellationToken)
    {
        var movimentacao = await _dbContext.MovimentacoesFinanceiras.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (movimentacao is null)
        {
            return NotFound();
        }

        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        movimentacao.Tipo = request.Tipo;
        movimentacao.CategoriaFinanceiraId = request.CategoriaFinanceiraId;
        movimentacao.ImovelId = request.ImovelId;
        movimentacao.ReservaId = request.ReservaId;
        movimentacao.ProprietarioId = request.ProprietarioId;
        movimentacao.Data = NormalizeDate(request.Data);
        movimentacao.Descricao = request.Descricao.Trim();
        movimentacao.Valor = request.Valor;
        movimentacao.Observacoes = request.Observacoes?.Trim();
        movimentacao.DataAtualizacao = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await BaseQuery().FirstAsync(m => m.Id == movimentacao.Id, cancellationToken);
        return Ok(ToResponse(updated));
    }

    [HttpDelete("movimentacoes/{id:int}")]
    public async Task<IActionResult> DeleteMovimentacao(int id, CancellationToken cancellationToken)
    {
        var movimentacao = await _dbContext.MovimentacoesFinanceiras.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (movimentacao is null)
        {
            return NotFound();
        }

        _dbContext.MovimentacoesFinanceiras.Remove(movimentacao);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("movimentacoes/{id:int}/serie")]
    public async Task<IActionResult> DeleteSerieMovimentacao(int id, CancellationToken cancellationToken)
    {
        var movimentacao = await _dbContext.MovimentacoesFinanceiras.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (movimentacao is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(movimentacao.GrupoRecorrenciaId))
        {
            _dbContext.MovimentacoesFinanceiras.Remove(movimentacao);
        }
        else
        {
            var serie = await _dbContext.MovimentacoesFinanceiras
                .Where(m => m.GrupoRecorrenciaId == movimentacao.GrupoRecorrenciaId)
                .ToListAsync(cancellationToken);
            _dbContext.MovimentacoesFinanceiras.RemoveRange(serie);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("fluxo-caixa")]
    public async Task<ActionResult<FluxoCaixaResponse>> GetFluxoCaixa(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? categoriaId,
        [FromQuery] int? imovelId,
        [FromQuery] int? proprietarioId,
        [FromQuery] int? reservaId,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(
            _dbContext.MovimentacoesFinanceiras.AsNoTracking().Include(m => m.CategoriaFinanceira).AsQueryable(),
            inicio,
            fim,
            null,
            categoriaId,
            imovelId,
            proprietarioId,
            reservaId);

        var entradas = await query
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita)
            .SumAsync(m => (decimal?)m.Valor, cancellationToken) ?? 0;

        var saidas = await query
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa)
            .SumAsync(m => (decimal?)m.Valor, cancellationToken) ?? 0;

        var categorias = await query
            .GroupBy(m => new { m.CategoriaFinanceiraId, CategoriaNome = m.CategoriaFinanceira!.Nome, m.Tipo })
            .Select(g => new
            {
                g.Key.CategoriaFinanceiraId,
                g.Key.CategoriaNome,
                g.Key.Tipo,
                Total = g.Sum(m => m.Valor)
            })
            .OrderBy(g => g.Tipo)
            .ThenByDescending(g => g.Total)
            .ToListAsync(cancellationToken);

        var porCategoria = categorias
            .Select(g => new FluxoCaixaCategoriaResponse(
                g.CategoriaFinanceiraId,
                g.CategoriaNome,
                g.Tipo,
                g.Total))
            .ToList();

        var dias = await query
            .GroupBy(m => m.Data.Date)
            .Select(g => new
            {
                Data = g.Key,
                Entradas = g.Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita).Sum(m => m.Valor),
                Saidas = g.Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa).Sum(m => m.Valor)
            })
            .OrderBy(g => g.Data)
            .ToListAsync(cancellationToken);

        var porDia = dias
            .Select(g => new FluxoCaixaDiaResponse(g.Data, g.Entradas, g.Saidas))
            .ToList();

        return Ok(new FluxoCaixaResponse(
            entradas,
            saidas,
            entradas - saidas,
            porCategoria,
            porDia));
    }

    private IQueryable<MovimentacaoFinanceira> BaseQuery()
    {
        return _dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Include(m => m.CategoriaFinanceira)
            .Include(m => m.Imovel)
            .Include(m => m.Proprietario)
            .Include(m => m.Reserva);
    }

    private static IQueryable<MovimentacaoFinanceira> ApplyFilters(
        IQueryable<MovimentacaoFinanceira> query,
        DateTime? inicio,
        DateTime? fim,
        MovimentacaoFinanceiraTipo? tipo,
        int? categoriaId,
        int? imovelId,
        int? proprietarioId,
        int? reservaId)
    {
        if (inicio.HasValue)
        {
            query = query.Where(m => m.Data >= NormalizeDate(inicio.Value));
        }

        if (fim.HasValue)
        {
            query = query.Where(m => m.Data <= NormalizeDate(fim.Value));
        }

        if (tipo.HasValue)
        {
            query = query.Where(m => m.Tipo == tipo.Value);
        }

        if (categoriaId.HasValue)
        {
            query = query.Where(m => m.CategoriaFinanceiraId == categoriaId.Value);
        }

        if (imovelId.HasValue)
        {
            query = query.Where(m => m.ImovelId == imovelId.Value);
        }

        if (proprietarioId.HasValue)
        {
            query = query.Where(m => m.ProprietarioId == proprietarioId.Value);
        }

        if (reservaId.HasValue)
        {
            query = query.Where(m => m.ReservaId == reservaId.Value);
        }

        return query;
    }

    private async Task<ActionResult?> ValidateRequestAsync(MovimentacaoFinanceiraRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Descricao))
        {
            return BadRequest(new { message = "Descrição da movimentação é obrigatória." });
        }

        if (request.Valor <= 0)
        {
            return BadRequest(new { message = "Valor da movimentação deve ser maior que zero." });
        }

        if (request.Recorrente && request.Tipo != MovimentacaoFinanceiraTipo.Despesa)
        {
            return BadRequest(new { message = "Recorrência está disponível apenas para despesas." });
        }

        if (request.Recorrente)
        {
            if (!request.RecorrenciaFrequencia.HasValue)
            {
                return BadRequest(new { message = "Selecione a frequência da recorrência." });
            }

            if (!request.RecorrenciaIntervalo.HasValue || request.RecorrenciaIntervalo.Value < 1)
            {
                return BadRequest(new { message = "Intervalo da recorrência deve ser de pelo menos 1 período." });
            }

            var hasParcelas = request.RecorrenciaQuantidadeParcelas.HasValue;
            var hasFim = request.RecorrenciaFim.HasValue;

            if (hasParcelas == hasFim)
            {
                return BadRequest(new { message = "Escolha quantidade de parcelas ou data final para encerrar a recorrência." });
            }

            if (hasParcelas && request.RecorrenciaQuantidadeParcelas!.Value < 2)
            {
                return BadRequest(new { message = "Informe pelo menos 2 parcelas para a recorrência." });
            }

            if (hasFim && NormalizeDate(request.RecorrenciaFim!.Value) <= NormalizeDate(request.Data))
            {
                return BadRequest(new { message = "A data final da recorrência deve ser posterior à data inicial." });
            }
        }

        var categoria = await _dbContext.CategoriasFinanceiras
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoriaFinanceiraId && c.Ativo, cancellationToken);

        if (categoria is null)
        {
            return BadRequest(new { message = "Categoria financeira ativa não encontrada." });
        }

        if (categoria.Tipo != request.Tipo)
        {
            return BadRequest(new { message = "Categoria financeira não corresponde ao tipo da movimentação." });
        }

        if (request.ImovelId.HasValue &&
            !await _dbContext.Imoveis.AnyAsync(i => i.Id == request.ImovelId.Value, cancellationToken))
        {
            return BadRequest(new { message = "Imóvel não encontrado." });
        }

        if (request.ProprietarioId.HasValue &&
            !await _dbContext.Proprietarios.AnyAsync(p => p.Id == request.ProprietarioId.Value, cancellationToken))
        {
            return BadRequest(new { message = "Sócio não encontrado." });
        }

        if (request.ReservaId.HasValue &&
            !await _dbContext.Reservas.AnyAsync(r => r.Id == request.ReservaId.Value, cancellationToken))
        {
            return BadRequest(new { message = "Reserva não encontrada." });
        }

        return null;
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static List<DateTime> BuildOccurrenceDates(
        DateTime dataBase,
        bool recorrente,
        MovimentacaoRecorrenciaFrequencia? frequencia,
        int? intervalo,
        int? quantidadeParcelas,
        DateTime? fim)
    {
        if (!recorrente)
        {
            return [dataBase];
        }

        var dates = new List<DateTime> { dataBase };
        var step = Math.Max(1, intervalo ?? 1);

        if (quantidadeParcelas.HasValue)
        {
            while (dates.Count < quantidadeParcelas.Value)
            {
                dates.Add(AdvanceDate(dates[^1], frequencia!.Value, step));
            }

            return dates;
        }

        var endDate = NormalizeDate(fim!.Value);
        var current = dataBase;
        while (true)
        {
            current = AdvanceDate(current, frequencia!.Value, step);
            if (current > endDate)
            {
                break;
            }

            dates.Add(current);
        }

        return dates;
    }

    private static DateTime AdvanceDate(DateTime baseDate, MovimentacaoRecorrenciaFrequencia frequencia, int intervalo)
    {
        return frequencia switch
        {
            MovimentacaoRecorrenciaFrequencia.Semanal => baseDate.AddDays(7 * intervalo),
            MovimentacaoRecorrenciaFrequencia.Anual => baseDate.AddYears(intervalo),
            _ => baseDate.AddMonths(intervalo)
        };
    }

    private static MovimentacaoFinanceiraResponse ToResponse(MovimentacaoFinanceira movimentacao)
    {
        return new MovimentacaoFinanceiraResponse(
            movimentacao.Id,
            movimentacao.Tipo,
            movimentacao.CategoriaFinanceiraId,
            movimentacao.CategoriaFinanceira?.Nome ?? string.Empty,
            movimentacao.ImovelId,
            movimentacao.Imovel?.Nome,
            movimentacao.ReservaId,
            movimentacao.ProprietarioId,
            movimentacao.Proprietario?.Nome,
            movimentacao.Data,
            movimentacao.Descricao,
            movimentacao.Valor,
            movimentacao.Observacoes,
            movimentacao.GrupoRecorrenciaId,
            movimentacao.ParcelaAtual,
            movimentacao.TotalParcelas,
            movimentacao.RecorrenciaFrequencia,
            movimentacao.RecorrenciaIntervalo,
            movimentacao.RecorrenciaFim,
            movimentacao.DataCriacao,
            movimentacao.DataAtualizacao);
    }
}

public sealed record MovimentacaoFinanceiraRequest(
    MovimentacaoFinanceiraTipo Tipo,
    int CategoriaFinanceiraId,
    int? ImovelId,
    int? ReservaId,
    int? ProprietarioId,
    DateTime Data,
    string Descricao,
    decimal Valor,
    string? Observacoes,
    bool Recorrente = false,
    MovimentacaoRecorrenciaFrequencia? RecorrenciaFrequencia = null,
    int? RecorrenciaIntervalo = null,
    int? RecorrenciaQuantidadeParcelas = null,
    DateTime? RecorrenciaFim = null);

public sealed record MovimentacaoFinanceiraResponse(
    int Id,
    MovimentacaoFinanceiraTipo Tipo,
    int CategoriaFinanceiraId,
    string CategoriaNome,
    int? ImovelId,
    string? ImovelNome,
    int? ReservaId,
    int? ProprietarioId,
    string? ProprietarioNome,
    DateTime Data,
    string Descricao,
    decimal Valor,
    string? Observacoes,
    string? GrupoRecorrenciaId,
    int? ParcelaAtual,
    int? TotalParcelas,
    MovimentacaoRecorrenciaFrequencia? RecorrenciaFrequencia,
    int? RecorrenciaIntervalo,
    DateTime? RecorrenciaFim,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);

public sealed record FluxoCaixaResponse(
    decimal Entradas,
    decimal Saidas,
    decimal Saldo,
    IReadOnlyCollection<FluxoCaixaCategoriaResponse> PorCategoria,
    IReadOnlyCollection<FluxoCaixaDiaResponse> PorDia);

public sealed record FluxoCaixaCategoriaResponse(
    int CategoriaFinanceiraId,
    string CategoriaNome,
    MovimentacaoFinanceiraTipo Tipo,
    decimal Total);

public sealed record FluxoCaixaDiaResponse(
    DateTime Data,
    decimal Entradas,
    decimal Saidas)
{
    public decimal Saldo => Entradas - Saidas;
}
