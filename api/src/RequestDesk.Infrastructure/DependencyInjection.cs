using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestDesk.Application.Common.Interfaces;
using RequestDesk.Infrastructure.Demo;
using RequestDesk.Infrastructure.Identity;
using RequestDesk.Infrastructure.Persistence;
using RequestDesk.Infrastructure.Persistence.Queries;
using RequestDesk.Infrastructure.Persistence.Repositories;
using RequestDesk.Infrastructure.Reporting;
using RequestDesk.Infrastructure.Storage;

namespace RequestDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<AppDbContext>(options => ConfigureDbContext(options, connectionString));

        services.AddIdentityCore<ApplicationUser>(identity =>
            {
                identity.User.RequireUniqueEmail = true;
                identity.Password.RequiredLength = 10;
                identity.Lockout.MaxFailedAccessAttempts = 10;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= JwtOptions.MinimumSigningKeyLength, $"Jwt:SigningKey must be at least {JwtOptions.MinimumSigningKeyLength} characters.")
            .ValidateOnStart();
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        services.AddOptions<DemoOptions>().Bind(configuration.GetSection(DemoOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IServiceRequestReadStore, ServiceRequestReadStore>();
        services.AddScoped<ISummaryReport, SummaryQuery>();
        services.AddScoped<IAuthenticator, IdentityAuthenticator>();
        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();

        services.AddScoped<DemoSeeder>();
        services.AddSingleton<DemoResetService>();
        services.AddHostedService(sp => sp.GetRequiredService<DemoResetService>());

        return services;
    }

    /// <summary>Shared by the runtime registration and the design-time factory so migrations see the same model.</summary>
    public static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AppendOnlyHistoryInterceptor());
    }
}
