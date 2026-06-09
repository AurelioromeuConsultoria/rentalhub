using RentalHub.Domain.Entities;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Services;

public sealed class SecurityAuditService
{
    private readonly RentalHubDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SecurityAuditService> _logger;

    public SecurityAuditService(
        RentalHubDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SecurityAuditService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task RecordAsync(
        string action,
        int? tenantId = null,
        string? entityId = null,
        string? userName = null,
        string? userEmail = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.AuditLogs.Add(new AuditLog
            {
                TenantId = tenantId.GetValueOrDefault(_dbContext.CurrentTenantId),
                EntityName = "Seguranca",
                EntityId = string.IsNullOrWhiteSpace(entityId) ? "auth" : entityId,
                Action = action,
                UserName = userName,
                UserEmail = userEmail,
                IpAddress = GetIpAddress(),
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record security audit event {Action}.", action);
        }
    }

    private string? GetIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;
        var forwardedFor = context?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context?.Connection.RemoteIpAddress?.ToString();
    }
}
