using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Services;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Services;

public sealed class EmailNotificationDigestHostedService : BackgroundService
{
    private const string DigestType = "digest-operacional";
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationDigestHostedService> _logger;

    public EmailNotificationDigestHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<EmailNotificationDigestHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("Notifications:Email:Enabled"))
        {
            _logger.LogInformation("RentalHub email notification digest is disabled.");
            return;
        }

        _logger.LogInformation("RentalHub email notification digest is enabled.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueDigestsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send RentalHub email notification digest.");
            }

            var intervalMinutes = Math.Clamp(
                _configuration.GetValue<int?>("Notifications:Email:IntervalMinutes") ?? 60,
                5,
                1440);
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task SendDueDigestsAsync(CancellationToken cancellationToken)
    {
        var localNow = GetLocalNow();
        var digestHour = Math.Clamp(_configuration.GetValue<int?>("Notifications:Email:DigestHour") ?? 8, 0, 23);
        if (localNow.Hour < digestHour)
        {
            return;
        }

        var referenceDate = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Utc);
        var horizonDays = Math.Clamp(_configuration.GetValue<int?>("Notifications:Email:HorizonDays") ?? 3, 1, 14);
        var newReservationHours = Math.Clamp(
            _configuration.GetValue<int?>("Notifications:Email:NewReservationHours") ?? 24,
            1,
            168);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentalHubDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<OperationalNotificationService>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var tenants = await dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tenant => tenant.Ativo)
            .OrderBy(tenant => tenant.NomeExibicao)
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            var notifications = await notificationService.GetAsync(
                new OperationalNotificationRequest(
                    tenant.Id,
                    IsOwner: false,
                    OwnerId: null,
                    horizonDays,
                    newReservationHours),
                cancellationToken);

            if (notifications.Count == 0)
            {
                continue;
            }

            var recipients = await GetRecipientsAsync(dbContext, tenant, cancellationToken);
            foreach (var recipient in recipients)
            {
                var alreadySent = await dbContext.EmailNotificationLogs
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(log => log.TenantId == tenant.Id &&
                        log.ReferenceDate == referenceDate &&
                        log.Type == DigestType &&
                        log.RecipientEmail == recipient,
                        cancellationToken);

                if (alreadySent)
                {
                    continue;
                }

                var subject = $"RentalHub: agenda operacional de {localNow:dd/MM/yyyy}";
                var adminUrl = GetAdminUrl();
                var htmlBody = BuildHtmlBody(tenant, notifications, adminUrl, localNow);
                var textBody = BuildTextBody(tenant, notifications, adminUrl, localNow);

                await emailSender.SendAsync(recipient, subject, htmlBody, textBody, cancellationToken);
                dbContext.EmailNotificationLogs.Add(new EmailNotificationLog
                {
                    TenantId = tenant.Id,
                    ReferenceDate = referenceDate,
                    Type = DigestType,
                    RecipientEmail = recipient,
                    Subject = subject,
                    SentAt = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyCollection<string>> GetRecipientsAsync(
        RentalHubDbContext dbContext,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_configuration.GetValue<bool?>("Notifications:Email:SendToOperationalEmail") ?? true)
        {
            AddEmail(recipients, tenant.EmailOperacional);
        }

        if (_configuration.GetValue<bool?>("Notifications:Email:SendToAdmins") ?? true)
        {
            var adminEmails = await dbContext.Usuarios
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => user.TenantId == tenant.Id &&
                    user.Ativo &&
                    user.TipoUsuario == TipoUsuario.Administrador)
                .Select(user => user.Email)
                .ToListAsync(cancellationToken);

            foreach (var email in adminEmails)
            {
                AddEmail(recipients, email);
            }
        }

        return recipients.ToList();
    }

    private DateTimeOffset GetLocalNow()
    {
        var timezoneId = _configuration["Notifications:Email:TimeZone"] ?? "America/Sao_Paulo";
        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Timezone {TimezoneId} was not found. Falling back to UTC.", timezoneId);
            return DateTimeOffset.UtcNow;
        }
        catch (InvalidTimeZoneException)
        {
            _logger.LogWarning("Timezone {TimezoneId} is invalid. Falling back to UTC.", timezoneId);
            return DateTimeOffset.UtcNow;
        }
    }

    private string GetAdminUrl()
    {
        return _configuration["Notifications:Email:AdminUrl"]
            ?? _configuration["RENTALHUB_PUBLIC_URL"]
            ?? "https://rentalhub.malachdigital.com.br";
    }

    private static void AddEmail(HashSet<string> recipients, string? email)
    {
        if (!string.IsNullOrWhiteSpace(email) && email.Contains('@', StringComparison.Ordinal))
        {
            recipients.Add(email.Trim());
        }
    }

    private static string BuildHtmlBody(
        Tenant tenant,
        IReadOnlyCollection<OperationalNotification> notifications,
        string adminUrl,
        DateTimeOffset localNow)
    {
        var groupedNotifications = notifications
            .GroupBy(notification => notification.Tipo)
            .OrderByDescending(group => group.Any(notification => notification.Prioridade == "alta"))
            .ThenBy(group => group.Key);

        var builder = new StringBuilder();
        builder.Append("""
            <div style="font-family:Arial,sans-serif;color:#1f2937;background:#f8fafc;padding:24px">
              <div style="max-width:720px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden">
                <div style="background:#0f172a;color:#ffffff;padding:24px">
                  <div style="font-size:13px;letter-spacing:.08em;text-transform:uppercase;color:#93c5fd">RentalHub</div>
                  <h1 style="margin:8px 0 0;font-size:24px">Resumo operacional</h1>
            """);
        builder.Append($"""
                  <p style="margin:8px 0 0;color:#cbd5e1">{Html(tenant.NomeExibicao)} · {localNow:dd/MM/yyyy}</p>
                </div>
                <div style="padding:24px">
                  <p style="margin:0 0 18px;color:#475569">Encontramos {notifications.Count} item(ns) que merecem atenção nos próximos dias.</p>
            """);

        foreach (var group in groupedNotifications)
        {
            builder.Append($"""
                  <h2 style="margin:22px 0 10px;font-size:16px;color:#0f172a">{Html(GetGroupTitle(group.Key))}</h2>
                  <table style="width:100%;border-collapse:collapse">
            """);

            foreach (var notification in group)
            {
                var badgeColor = notification.Prioridade == "alta" ? "#b91c1c" : "#2563eb";
                builder.Append($"""
                    <tr>
                      <td style="border-top:1px solid #e5e7eb;padding:12px 0;vertical-align:top">
                        <div style="font-weight:700;color:#111827">{Html(notification.Titulo)}</div>
                        <div style="margin-top:4px;color:#475569">{Html(notification.Descricao)}</div>
                      </td>
                      <td style="border-top:1px solid #e5e7eb;padding:12px 0;text-align:right;white-space:nowrap;vertical-align:top">
                        <div style="font-weight:700;color:#111827">{notification.Data:dd/MM/yyyy}</div>
                        <div style="display:inline-block;margin-top:6px;padding:3px 8px;border-radius:999px;background:{badgeColor};color:#ffffff;font-size:12px">{Html(notification.Prioridade)}</div>
                      </td>
                    </tr>
                """);
            }

            builder.Append("      </table>");
        }

        builder.Append($"""
                  <div style="margin-top:24px">
                    <a href="{Html(adminUrl)}" style="display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:8px;padding:12px 16px;font-weight:700">Abrir RentalHub</a>
                  </div>
                  <p style="margin:24px 0 0;color:#64748b;font-size:12px">Este e-mail é automático. As regras de envio podem ser ajustadas nas configurações do servidor.</p>
                </div>
              </div>
            </div>
            """);

        return builder.ToString();
    }

    private static string BuildTextBody(
        Tenant tenant,
        IReadOnlyCollection<OperationalNotification> notifications,
        string adminUrl,
        DateTimeOffset localNow)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"RentalHub - resumo operacional de {localNow:dd/MM/yyyy}");
        builder.AppendLine(tenant.NomeExibicao);
        builder.AppendLine();

        foreach (var notification in notifications)
        {
            builder.AppendLine($"[{notification.Prioridade}] {notification.Titulo} - {notification.Data:dd/MM/yyyy}");
            builder.AppendLine(notification.Descricao);
            builder.AppendLine();
        }

        builder.AppendLine($"Acesse: {adminUrl}");
        return builder.ToString();
    }

    private static string GetGroupTitle(string type)
    {
        return type switch
        {
            "nova-reserva" => "Novas reservas",
            "checkin" => "Check-ins próximos",
            "checkout" => "Check-outs próximos",
            "limpeza" => "Limpezas pendentes",
            "manutencao" => "Manutenções pendentes",
            "repasse" => "Repasses pendentes",
            _ => "Outras notificações"
        };
    }

    private static string Html(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
