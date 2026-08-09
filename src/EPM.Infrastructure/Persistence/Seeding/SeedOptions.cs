namespace EPM.Infrastructure.Persistence.Seeding;

/// <summary>
/// Controls the demo data written on first run, bound from the "Seed" section.
/// </summary>
/// <remarks>
/// The passwords have no defaults on purpose. A hardcoded fallback is the kind of thing that
/// survives into production unnoticed — if these are not configured, seeding of the login
/// accounts is skipped and says so in the log.
/// </remarks>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Turned off in production deployments; the docker-compose stack leaves it on.</summary>
    public bool Enabled { get; init; } = true;

    public string? AdminPassword { get; init; }

    public string? ManagerPassword { get; init; }

    public string? UserPassword { get; init; }

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(AdminPassword)
        && !string.IsNullOrWhiteSpace(ManagerPassword)
        && !string.IsNullOrWhiteSpace(UserPassword);
}
