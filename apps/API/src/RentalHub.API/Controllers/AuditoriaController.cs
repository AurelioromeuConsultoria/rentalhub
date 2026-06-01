using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Common;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class AuditoriaController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public AuditoriaController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> Get(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] string? entidade,
        [FromQuery] string? acao,
        [FromQuery] string? usuario,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (inicio.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= NormalizeDate(inicio.Value));
        }

        if (fim.HasValue)
        {
            query = query.Where(log => log.CreatedAt < NormalizeDate(fim.Value).AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(entidade))
        {
            var term = entidade.Trim().ToLowerInvariant();
            query = query.Where(log => log.EntityName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(acao))
        {
            var term = acao.Trim().ToLowerInvariant();
            query = query.Where(log => log.Action.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            var term = usuario.Trim().ToLowerInvariant();
            query = query.Where(log =>
                (log.UserName != null && log.UserName.ToLower().Contains(term)) ||
                (log.UserEmail != null && log.UserEmail.ToLower().Contains(term)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AuditLogResponse(
                log.Id,
                log.EntityName,
                log.EntityId,
                log.Action,
                log.UserName,
                log.UserEmail,
                log.IpAddress,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<AuditLogResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
    }
}

public sealed record AuditLogResponse(
    long Id,
    string EntityName,
    string EntityId,
    string Action,
    string? UserName,
    string? UserEmail,
    string? IpAddress,
    DateTime CreatedAt);
