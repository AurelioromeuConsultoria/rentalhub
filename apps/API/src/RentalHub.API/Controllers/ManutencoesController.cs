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
public sealed class ManutencoesController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public ManutencoesController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ManutencaoResponse>>> GetAll(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] int? imovelId,
        [FromQuery] ManutencaoStatus? status,
        [FromQuery] string? categoria,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Manutencoes
            .AsNoTracking()
            .Include(m => m.Imovel)
            .AsQueryable();

        if (inicio.HasValue)
        {
            var start = NormalizeDate(inicio.Value);
            query = query.Where(m => m.DataAbertura >= start || (m.DataPrevista.HasValue && m.DataPrevista.Value >= start));
        }

        if (fim.HasValue)
        {
            var end = NormalizeDate(fim.Value);
            query = query.Where(m => m.DataAbertura <= end || (m.DataPrevista.HasValue && m.DataPrevista.Value <= end));
        }

        if (imovelId.HasValue)
        {
            query = query.Where(m => m.ImovelId == imovelId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            var normalizedCategoria = categoria.Trim().ToLower();
            query = query.Where(m => m.Categoria.ToLower().Contains(normalizedCategoria));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.Status == ManutencaoStatus.Aberta || m.Status == ManutencaoStatus.EmAndamento)
            .ThenBy(m => m.DataPrevista ?? m.DataAbertura)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => ToResponse(m))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<ManutencaoResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ManutencaoResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var manutencao = await _dbContext.Manutencoes
            .AsNoTracking()
            .Include(m => m.Imovel)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        return manutencao is null ? NotFound() : Ok(ToResponse(manutencao));
    }

    [HttpPost]
    public async Task<ActionResult<ManutencaoResponse>> Create(ManutencaoRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var manutencao = new Manutencao
        {
            TenantId = _dbContext.CurrentTenantId,
            ImovelId = request.ImovelId,
            Categoria = request.Categoria.Trim(),
            Descricao = request.Descricao.Trim(),
            Responsavel = request.Responsavel?.Trim(),
            DataAbertura = NormalizeDate(request.DataAbertura),
            DataPrevista = request.DataPrevista.HasValue ? NormalizeDate(request.DataPrevista.Value) : null,
            DataResolucao = request.DataResolucao.HasValue ? NormalizeDate(request.DataResolucao.Value) : null,
            ValorEstimado = request.ValorEstimado,
            ValorRealizado = request.ValorRealizado,
            Status = request.Status,
            Observacoes = request.Observacoes?.Trim(),
            DataCriacao = DateTime.UtcNow
        };

        _dbContext.Manutencoes.Add(manutencao);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.Manutencoes.AsNoTracking().Include(m => m.Imovel).FirstAsync(m => m.Id == manutencao.Id, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = manutencao.Id }, ToResponse(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ManutencaoResponse>> Update(int id, ManutencaoRequest request, CancellationToken cancellationToken)
    {
        var manutencao = await _dbContext.Manutencoes.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (manutencao is null)
        {
            return NotFound();
        }

        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        manutencao.ImovelId = request.ImovelId;
        manutencao.Categoria = request.Categoria.Trim();
        manutencao.Descricao = request.Descricao.Trim();
        manutencao.Responsavel = request.Responsavel?.Trim();
        manutencao.DataAbertura = NormalizeDate(request.DataAbertura);
        manutencao.DataPrevista = request.DataPrevista.HasValue ? NormalizeDate(request.DataPrevista.Value) : null;
        manutencao.DataResolucao = request.DataResolucao.HasValue ? NormalizeDate(request.DataResolucao.Value) : null;
        manutencao.ValorEstimado = request.ValorEstimado;
        manutencao.ValorRealizado = request.ValorRealizado;
        manutencao.Status = request.Status;
        manutencao.Observacoes = request.Observacoes?.Trim();
        manutencao.DataAtualizacao = DateTime.UtcNow;

        if (manutencao.Status == ManutencaoStatus.Resolvida && !manutencao.DataResolucao.HasValue)
        {
            manutencao.DataResolucao = DateTime.UtcNow.Date;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await _dbContext.Manutencoes.AsNoTracking().Include(m => m.Imovel).FirstAsync(m => m.Id == manutencao.Id, cancellationToken);
        return Ok(ToResponse(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var manutencao = await _dbContext.Manutencoes.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (manutencao is null)
        {
            return NotFound();
        }

        manutencao.Status = ManutencaoStatus.Cancelada;
        manutencao.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult?> ValidateRequestAsync(ManutencaoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Categoria) || string.IsNullOrWhiteSpace(request.Descricao))
        {
            return BadRequest(new { message = "Categoria e descrição são obrigatórias." });
        }

        if (request.ValorEstimado < 0 || request.ValorRealizado < 0)
        {
            return BadRequest(new { message = "Valores de manutenção não podem ser negativos." });
        }

        var imovelExists = await _dbContext.Imoveis.AnyAsync(
            i => i.Id == request.ImovelId && i.Status != ImovelStatus.Inativo,
            cancellationToken);

        if (!imovelExists)
        {
            return BadRequest(new { message = "Imóvel ativo não encontrado." });
        }

        if (request.Status == ManutencaoStatus.Resolvida && request.ValorRealizado <= 0)
        {
            return BadRequest(new { message = "Informe o valor realizado para resolver a manutenção." });
        }

        return null;
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }

    private static ManutencaoResponse ToResponse(Manutencao manutencao)
    {
        return new ManutencaoResponse(
            manutencao.Id,
            manutencao.ImovelId,
            manutencao.Imovel?.Nome ?? string.Empty,
            manutencao.Categoria,
            manutencao.Descricao,
            manutencao.Responsavel,
            manutencao.DataAbertura,
            manutencao.DataPrevista,
            manutencao.DataResolucao,
            manutencao.ValorEstimado,
            manutencao.ValorRealizado,
            manutencao.Status,
            manutencao.Observacoes,
            manutencao.DataCriacao,
            manutencao.DataAtualizacao);
    }
}

public sealed record ManutencaoRequest(
    int ImovelId,
    string Categoria,
    string Descricao,
    string? Responsavel,
    DateTime DataAbertura,
    DateTime? DataPrevista,
    DateTime? DataResolucao,
    decimal ValorEstimado,
    decimal ValorRealizado,
    ManutencaoStatus Status,
    string? Observacoes);

public sealed record ManutencaoResponse(
    int Id,
    int ImovelId,
    string ImovelNome,
    string Categoria,
    string Descricao,
    string? Responsavel,
    DateTime DataAbertura,
    DateTime? DataPrevista,
    DateTime? DataResolucao,
    decimal ValorEstimado,
    decimal ValorRealizado,
    ManutencaoStatus Status,
    string? Observacoes,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);
