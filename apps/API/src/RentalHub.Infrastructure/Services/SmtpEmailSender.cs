using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentalHub.Application.Services;

namespace RentalHub.Infrastructure.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"];
        var from = _configuration["Smtp:From"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            _logger.LogInformation("SMTP not configured. Email to {Email} with subject {Subject} was skipped.", to, subject);
            return;
        }

        var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
        var enableSsl = bool.TryParse(_configuration["Smtp:EnableSsl"], out var configuredSsl) ? configuredSsl : true;
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var fromName = _configuration["Smtp:FromName"] ?? "RentalHub";

        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var cancellationRegistration = cancellationToken.Register(client.SendAsyncCancel);
        await client.SendMailAsync(message);
    }
}
