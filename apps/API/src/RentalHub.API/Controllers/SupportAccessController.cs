using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.API.Services;
using RentalHub.Application.Security;
using RentalHub.Domain.Entities;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/support-access")]
public sealed class SupportAccessController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;
    private readonly SecurityAuditService _securityAudit;

    public SupportAccessController(RentalHubDbContext dbContext, SecurityAuditService securityAudit)
    {
        _dbContext = dbContext;
        _securityAudit = securityAudit;
    }

    [HttpPost("start")]
    public async Task<ActionResult<SupportAccessSessionResponse>> Start(
        StartSupportAccessRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var reason = request.Motivo?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
        {
            return BadRequest(new { message = "Informe um motivo com pelo menos 10 caracteres para acessar os dados do cliente." });
        }

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return NotFound(new { message = "Empresa não encontrada." });
        }

        var now = DateTime.UtcNow;
        var token = GenerateToken();
        var session = new SupportAccessSession
        {
            TenantId = tenant.Id,
            UsuarioId = userId.Value,
            TokenHash = HashToken(token),
            Motivo = reason,
            ExpiraEm = now.AddHours(2),
            DataCriacao = now
        };

        _dbContext.SupportAccessSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _securityAudit.RecordAsync(
            "SuporteIniciado",
            tenant.Id,
            $"tenant:{tenant.Id}",
            User.FindFirstValue(ClaimTypes.Name),
            User.FindFirstValue(ClaimTypes.Email),
            cancellationToken);

        return Ok(new SupportAccessSessionResponse(
            session.Id,
            tenant.Id,
            tenant.Slug,
            tenant.NomeExibicao,
            token,
            session.Motivo,
            session.ExpiraEm));
    }

    [HttpPost("end")]
    public async Task<IActionResult> End(EndSupportAccessRequest request, CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue || string.IsNullOrWhiteSpace(request.Token))
        {
            return NoContent();
        }

        var tokenHash = HashToken(request.Token);
        var session = await _dbContext.SupportAccessSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item =>
                item.TokenHash == tokenHash &&
                item.UsuarioId == userId.Value &&
                item.EncerradoEm == null,
                cancellationToken);

        if (session is null)
        {
            return NoContent();
        }

        session.EncerradoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _securityAudit.RecordAsync(
            "SuporteEncerrado",
            session.TenantId,
            $"tenant:{session.TenantId}",
            User.FindFirstValue(ClaimTypes.Name),
            User.FindFirstValue(ClaimTypes.Email),
            cancellationToken);

        return NoContent();
    }

    private bool IsPlatformAdmin()
    {
        return PlatformAdminClaims.IsPlatformAdmin(User);
    }

    private int? GetCurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
    }

    private static string GenerateToken()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

public sealed record StartSupportAccessRequest(int TenantId, string? Motivo);

public sealed record EndSupportAccessRequest(string? Token);

public sealed record SupportAccessSessionResponse(
    int Id,
    int TenantId,
    string TenantSlug,
    string TenantNome,
    string Token,
    string Motivo,
    DateTime ExpiraEm);
