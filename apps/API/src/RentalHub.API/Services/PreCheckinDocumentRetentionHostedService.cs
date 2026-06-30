using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Entities;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Services;

public sealed class PreCheckinDocumentRetentionHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PreCheckinDocumentRetentionHostedService> _logger;

    public PreCheckinDocumentRetentionHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<PreCheckinDocumentRetentionHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Privacy:PreCheckinDocuments:RetentionEnabled", true))
        {
            _logger.LogInformation("Pre-check-in document retention is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredDocumentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not purge expired pre-check-in documents.");
            }

            var intervalHours = Math.Clamp(
                _configuration.GetValue<int?>("Privacy:PreCheckinDocuments:RetentionSweepHours") ?? 24,
                1,
                168);

            await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
        }
    }

    private async Task PurgeExpiredDocumentsAsync(CancellationToken cancellationToken)
    {
        var retentionDays = Math.Clamp(
            _configuration.GetValue<int?>("Privacy:PreCheckinDocuments:RetentionDays") ?? 90,
            1,
            3650);
        var batchSize = Math.Clamp(
            _configuration.GetValue<int?>("Privacy:PreCheckinDocuments:RetentionBatchSize") ?? 200,
            10,
            1000);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentalHubDbContext>();

        var documents = await dbContext.ReservaHospedesCadastro
            .IgnoreQueryFilters()
            .Include(item => item.ReservaPreCheckin)
                .ThenInclude(preCheckin => preCheckin!.Reserva)
            .Where(item =>
                item.FotoDocumentoUrl != null &&
                item.ReservaPreCheckin != null &&
                item.ReservaPreCheckin.Reserva != null &&
                item.ReservaPreCheckin.Reserva.CheckOut < cutoff)
            .OrderBy(item => item.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (documents.Count == 0)
        {
            return;
        }

        var deletedFiles = 0;
        var now = DateTime.UtcNow;
        foreach (var document in documents)
        {
            var url = document.FotoDocumentoUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (TryDeleteUploadedFile(url))
            {
                deletedFiles++;
            }

            await RemoveDocumentUrlFromGuestNotesAsync(dbContext, document.TenantId, url, cancellationToken);
            document.FotoDocumentoUrl = null;
            document.ObservacoesAnalise = BuildRetentionNote(document.ObservacoesAnalise, retentionDays, now);
            document.DataAtualizacao = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Purged {Count} expired pre-check-in document references and deleted {DeletedFiles} files older than {RetentionDays} days after checkout.",
            documents.Count,
            deletedFiles,
            retentionDays);
    }

    private bool TryDeleteUploadedFile(string url)
    {
        if (!url.StartsWith("/uploads/tenants/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(webRootPath, relativePath));
        var uploadsRoot = Path.GetFullPath(Path.Combine(webRootPath, "uploads"));

        if (!fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            File.Delete(fullPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete expired pre-check-in document file {FilePath}.", fullPath);
            return false;
        }
    }

    private static async Task RemoveDocumentUrlFromGuestNotesAsync(
        RentalHubDbContext dbContext,
        int tenantId,
        string url,
        CancellationToken cancellationToken)
    {
        var guests = await dbContext.Hospedes
            .IgnoreQueryFilters()
            .Where(guest => guest.TenantId == tenantId &&
                guest.Observacoes != null &&
                guest.Observacoes.Contains(url))
            .ToListAsync(cancellationToken);

        foreach (var guest in guests)
        {
            guest.Observacoes = guest.Observacoes?.Replace(url, "documento expurgado por retenção", StringComparison.Ordinal);
            guest.DataAtualizacao = DateTime.UtcNow;
        }
    }

    private static string BuildRetentionNote(string? currentNote, int retentionDays, DateTime now)
    {
        var note = $"Documento expurgado automaticamente em {now:yyyy-MM-dd} após {retentionDays} dias do checkout.";
        if (string.IsNullOrWhiteSpace(currentNote))
        {
            return note;
        }

        var combined = $"{currentNote.Trim()} {note}";
        return combined.Length <= 1000 ? combined : combined[^1000..];
    }
}
