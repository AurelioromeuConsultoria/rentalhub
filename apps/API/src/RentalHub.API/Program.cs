using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using RentalHub.API.Health;
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
builder.Services.AddHostedService<DatabaseInitializerHostedService>();

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

builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Admin");
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
        path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);

    if (isOwner && path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) && !isAllowedOwnerPath)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Acesso restrito ao portal do proprietário." });
        return;
    }

    await next();
});
app.UseAuthorization();

app.MapControllers();

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
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };

        await context.Response.WriteAsJsonAsync(payload);
    }
});

app.Run();
