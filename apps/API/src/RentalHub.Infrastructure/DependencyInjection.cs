using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentalHub.Application.Security;
using RentalHub.Application.Services;
using RentalHub.Infrastructure.Data;
using RentalHub.Infrastructure.Security;

namespace RentalHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRentalHubInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddScoped<ITenantContext, DefaultTenantContext>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();

        services.AddDbContext<RentalHubDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(10);
                npgsqlOptions.EnableRetryOnFailure(2, TimeSpan.FromSeconds(1), null);
            });
        });

        return services;
    }
}
