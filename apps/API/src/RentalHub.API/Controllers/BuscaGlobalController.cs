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
public sealed class BuscaGlobalController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public BuscaGlobalController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<BuscaGlobalItemResponse>>> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Ok(Array.Empty<BuscaGlobalItemResponse>());
        }

        var term = q.Trim().ToLowerInvariant();
        var numericTerm = int.TryParse(term, out var parsedNumericTerm) ? parsedNumericTerm : (int?)null;
        var isOwner = string.Equals(User.FindFirstValue("TipoUsuario"), "4", StringComparison.Ordinal);
        var ownerId = int.TryParse(User.FindFirstValue("ProprietarioId"), out var parsedOwnerId) ? parsedOwnerId : (int?)null;
        var results = new List<BuscaGlobalItemResponse>();

        var imoveisQuery = _dbContext.Imoveis.AsNoTracking().AsQueryable();
        if (isOwner)
        {
            if (!ownerId.HasValue)
            {
                return Forbid();
            }

            imoveisQuery = imoveisQuery.Where(i => i.ProprietarioId == ownerId.Value);
        }

        var imoveis = await imoveisQuery
            .Where(i => i.Nome.ToLower().Contains(term) || i.CodigoInterno.ToLower().Contains(term))
            .OrderBy(i => i.Nome)
            .Take(6)
            .Select(i => new BuscaGlobalItemResponse(
                $"imovel-{i.Id}",
                "Imóvel",
                i.Nome,
                i.CodigoInterno,
                isOwner ? "/portal-proprietario" : "/imoveis"))
            .ToListAsync(cancellationToken);
        results.AddRange(imoveis);

        var ownerImovelIds = Array.Empty<int>();
        if (isOwner && ownerId.HasValue)
        {
            ownerImovelIds = await _dbContext.Imoveis
                .AsNoTracking()
                .Where(i => i.ProprietarioId == ownerId.Value)
                .Select(i => i.Id)
                .ToArrayAsync(cancellationToken);
        }

        var reservasQuery = _dbContext.Reservas.AsNoTracking().Include(r => r.Imovel).Include(r => r.Hospede).AsQueryable();
        if (isOwner)
        {
            reservasQuery = reservasQuery.Where(r => ownerImovelIds.Contains(r.ImovelId));
        }

        var reservas = await reservasQuery
            .Where(r => (numericTerm.HasValue && r.Id == numericTerm.Value) ||
                (r.Imovel != null && r.Imovel.Nome.ToLower().Contains(term)) ||
                (r.Hospede != null && r.Hospede.Nome.ToLower().Contains(term)))
            .OrderByDescending(r => r.CheckIn)
            .Take(6)
            .Select(r => new BuscaGlobalItemResponse(
                $"reserva-{r.Id}",
                "Reserva",
                $"Reserva #{r.Id}",
                r.Imovel == null ? string.Empty : r.Imovel.Nome,
                isOwner ? "/portal-proprietario" : "/reservas"))
            .ToListAsync(cancellationToken);
        results.AddRange(reservas);

        if (!isOwner)
        {
            var proprietarios = await _dbContext.Proprietarios
                .AsNoTracking()
                .Where(p => p.Nome.ToLower().Contains(term) || p.Documento.ToLower().Contains(term))
                .OrderBy(p => p.Nome)
                .Take(4)
                .Select(p => new BuscaGlobalItemResponse(
                    $"proprietario-{p.Id}",
                    "Proprietário",
                    p.Nome,
                    p.Documento,
                    "/proprietarios"))
                .ToListAsync(cancellationToken);
            results.AddRange(proprietarios);

            var hospedes = await _dbContext.Hospedes
                .AsNoTracking()
                .Where(h => h.Nome.ToLower().Contains(term) || (h.Email != null && h.Email.ToLower().Contains(term)))
                .OrderBy(h => h.Nome)
                .Take(4)
                .Select(h => new BuscaGlobalItemResponse(
                    $"hospede-{h.Id}",
                    "Hóspede",
                    h.Nome,
                    h.Email,
                    "/hospedes"))
                .ToListAsync(cancellationToken);
            results.AddRange(hospedes);
        }

        var repassesQuery = _dbContext.RepassesProprietarios.AsNoTracking().Include(r => r.Proprietario).AsQueryable();
        if (isOwner)
        {
            repassesQuery = repassesQuery.Where(r => r.ProprietarioId == ownerId!.Value);
        }

        var repasses = await repassesQuery
            .Where(r => (numericTerm.HasValue && r.Id == numericTerm.Value) ||
                (r.Proprietario != null && r.Proprietario.Nome.ToLower().Contains(term)))
            .OrderByDescending(r => r.PeriodoFim)
            .Take(4)
            .Select(r => new BuscaGlobalItemResponse(
                $"repasse-{r.Id}",
                "Repasse",
                $"Repasse #{r.Id}",
                r.Status.ToString(),
                isOwner ? "/portal-proprietario" : "/repasses"))
            .ToListAsync(cancellationToken);
        results.AddRange(repasses);

        return Ok(results.Take(20).ToList());
    }
}

public sealed record BuscaGlobalItemResponse(
    string Id,
    string Tipo,
    string Titulo,
    string? Descricao,
    string Href);
