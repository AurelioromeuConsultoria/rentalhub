using Microsoft.Extensions.Diagnostics.HealthChecks;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly RentalHubDbContext _dbContext;

    public DatabaseHealthCheck(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("PostgreSQL connection is available.")
            : HealthCheckResult.Unhealthy("PostgreSQL connection is unavailable.");
    }
}

