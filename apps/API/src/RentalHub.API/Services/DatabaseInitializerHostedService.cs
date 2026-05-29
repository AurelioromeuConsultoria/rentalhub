using Microsoft.EntityFrameworkCore;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Services;

public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseInitializerHostedService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializerHostedService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var ensureCreated = _configuration.GetValue("Database:EnsureCreatedOnStartup", true);
        if (!ensureCreated)
        {
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RentalHubDbContext>();
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            _logger.LogInformation("RentalHub database schema is ready.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize RentalHub database schema. The API will continue running.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

