using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RentalHub.API.Health;

public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly IWebHostEnvironment _environment;

    public StorageHealthCheck(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadsPath = Path.Combine(webRoot, "uploads");
        var probePath = Path.Combine(uploadsPath, ".healthcheck");

        try
        {
            Directory.CreateDirectory(uploadsPath);
            await File.WriteAllTextAsync(probePath, DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
            File.Delete(probePath);

            return HealthCheckResult.Healthy("Upload storage is writable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Upload storage is not writable.", ex);
        }
    }
}
