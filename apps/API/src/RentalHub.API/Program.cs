using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using RentalHub.API.Health;
using RentalHub.API.Middleware;
using RentalHub.API.Security;
using RentalHub.API.Services;
using RentalHub.Application.Services;
using RentalHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Admin", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:5173"];

        policy
            .SetIsOriginAllowed(origin =>
            {
                if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                return Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                       (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                        uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase));
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddRentalHubInfrastructure(builder.Configuration);
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddScoped<OperationalNotificationService>();
builder.Services.AddScoped<PasswordPolicyService>();
builder.Services.AddScoped<SecurityAuditService>();
builder.Services.AddHostedService<DatabaseInitializerHostedService>();
builder.Services.AddHostedService<EmailNotificationDigestHostedService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RentalHub";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "RentalHub";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-sensitive", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{GetRateLimitKey(context)}:{context.Request.Path}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"])
    .AddCheck<StorageHealthCheck>(
        "storage",
        failureStatus: HealthStatus.Degraded,
        tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Admin");
app.UseMiddleware<ErrorHandlingMiddleware>();
var webRootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath)
});
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var isOwner = string.Equals(context.User.FindFirst("TipoUsuario")?.Value, "4", StringComparison.Ordinal);
    var path = context.Request.Path;
    var isAllowedOwnerPath =
        path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/portalproprietario", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/notificacoes", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/buscaglobal", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/sistema", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/suporte", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);

    if (isOwner && path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) && !isAllowedOwnerPath)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Acesso restrito ao portal do proprietário." });
        return;
    }

    await next();
});
app.UseRentalHubPermissions();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/health/live", (IHostEnvironment environment) => Results.Ok(new
{
    status = "Healthy",
    environment = environment.EnvironmentName,
    checkedAt = DateTimeOffset.UtcNow,
    version = typeof(Program).Assembly.GetName().Version?.ToString()
})).AllowAnonymous();

app.MapGet("/", () => Results.Ok(new
{
    name = "RentalHub API",
    status = "running",
    docs = "/openapi/v1.json",
    health = "/api/health"
}));

app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            checkedAt = DateTimeOffset.UtcNow,
            environment = app.Environment.EnvironmentName,
            version = typeof(Program).Assembly.GetName().Version?.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                tags = entry.Value.Tags
            })
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
});

app.Run();

static string GetRateLimitKey(HttpContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(forwardedFor))
    {
        return forwardedFor.Split(',')[0].Trim();
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
