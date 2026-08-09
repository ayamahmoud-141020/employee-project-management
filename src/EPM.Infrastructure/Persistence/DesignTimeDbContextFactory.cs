using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EPM.Infrastructure.Persistence;

/// <summary>
/// Used by `dotnet ef` when it needs a context but not a running application.
/// </summary>
/// <remarks>
/// Without this, the tooling boots the API's whole DI container just to find a DbContext,
/// which means migrations cannot be generated unless a real connection string, a signing key
/// and every other startup requirement happen to be present. Generating a migration is an
/// offline operation — it reads the model, not the database — so a placeholder connection
/// string is enough. Set EPM_MIGRATIONS_CONNECTION to point at a real server when running
/// `database update` from here.
/// </remarks>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EPM_MIGRATIONS_CONNECTION")
                               ?? "Server=localhost,14330;Database=EmployeeProjectManagement;User Id=sa;" +
                                  "Password=placeholder;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sqlServer =>
                sqlServer.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
