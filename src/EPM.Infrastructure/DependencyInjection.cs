using EPM.Application.Abstractions;
using EPM.Infrastructure.Identity;
using EPM.Infrastructure.Persistence;
using EPM.Infrastructure.Persistence.Interceptors;
using EPM.Infrastructure.Persistence.Seeding;
using EPM.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EPM.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // ICurrentUser reads the request's claims, so it needs the accessor and has to be
        // scoped — a singleton would capture whichever request happened to resolve it first.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();

        services.AddApplicationAuthentication(configuration);
        services.AddApplicationAuthorization();

        services.AddSingleton<ISsoConfigurationProvider, EntraIdSsoConfigurationProvider>();

        services.AddScoped<DatabaseSeeder>();
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            // Resolved from the container rather than captured from the `configuration`
            // argument. Those are not always the same object: a host that layers extra
            // configuration on after service registration — WebApplicationFactory in the
            // integration tests, or an env-var provider added late — would otherwise be
            // ignored here, and the context would quietly connect to the wrong database.
            var connectionString = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured. " +
                    "Set ConnectionStrings:Default via user-secrets, environment or appsettings.");
            }

            options.UseSqlServer(connectionString, sqlServer =>
            {
                // Migrations live in this assembly, not the startup project, so EF has to be
                // told where to look when the two differ.
                sqlServer.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                // SQL Server in a container is briefly unavailable while it starts, and a
                // transient failure at that moment should not take the API down with it.
                sqlServer.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), null);
            });

            options.AddInterceptors(serviceProvider.GetRequiredService<DispatchDomainEventsInterceptor>());
        });

        services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());

        return services;
    }
}
