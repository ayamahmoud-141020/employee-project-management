using EPM.Infrastructure.Persistence;
using EPM.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EPM.Api.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Brings the database up to date and seeds it, for development only.
    /// </summary>
    /// <remarks>
    /// Convenient here — `docker compose up` gives a working stack with no extra step — but
    /// deliberately not called in production. Migrating from inside the application means
    /// every instance races to apply the same schema change on deploy, and a failed migration
    /// takes the app down with it. Production applies migrations as a separate step:
    /// `dotnet ef database update`, or a generated SQL script through the normal release
    /// process.
    /// </remarks>
    public static async Task ApplyMigrationsAndSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync();
        }
        catch (Exception exception)
        {
            // Logged rather than rethrown: a developer with no database running should still
            // get a booted API and a clear message, not a stack trace at startup.
            logger.LogError(
                exception,
                "Could not migrate or seed the database. The API will start, but data endpoints will fail. " +
                "Check that SQL Server is reachable on the configured connection string.");
        }
    }
}
