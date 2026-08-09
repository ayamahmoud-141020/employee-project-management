using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EPM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace EPM.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API against a throwaway SQL Server container.
/// </summary>
/// <remarks>
/// A real database, not SQLite and not the InMemory provider. These tests exist to catch the
/// things only the production engine can tell you about: whether the migrations actually
/// apply, whether a filtered unique index is valid T-SQL, whether a LINQ query translates.
/// Two of those have already bitten this codebase — see GetProjectEmployeesHandlerTests.
///
/// Shared across every test class in the assembly (see ApiCollection) because starting SQL
/// Server takes tens of seconds and doing it per class would dominate the run.
/// </remarks>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image the compose stack uses, so tests and local development agree on the engine.
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Integration#Test123")
        .Build();

    /// <summary>
    /// Matches the API's own JSON settings, so a response deserialises here exactly as it
    /// would in the Angular client — enums as strings included.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Forces the host to build now rather than on the first request, so migrations run
        // and any startup misconfiguration fails here instead of inside an unrelated test.
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _database.GetConnectionString(),

                // A fixed signing key: these tests only need it to be valid and stable, and
                // hardcoding it here keeps them from depending on a developer's user-secrets.
                ["Jwt:Key"] = "integration-tests-signing-key-not-used-anywhere-else-0123456789",
                ["Jwt:Issuer"] = "epm-api",
                ["Jwt:Audience"] = "epm-client",

                // The API seeds on startup in Development only, so these tests seed
                // deliberately per class instead — see DatabaseFixtures.
                ["Seed:Enabled"] = "false",

                ["EntraId:Enabled"] = "false",
            });
        });
    }

    /// <summary>An unauthenticated client, for testing that endpoints actually require a token.</summary>
    public HttpClient CreateAnonymousClient() => CreateClient();

    /// <summary>
    /// Signs in as the given seeded account and returns a client with the bearer token
    /// attached, so tests read as "as an admin, do X" rather than as token plumbing.
    /// </summary>
    public async Task<HttpClient> CreateClientAsAsync(string email, string password)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginPayload>>(Json);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload!.Data!.AccessToken);

        return client;
    }

    public new async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }

    private sealed record LoginPayload(string AccessToken);
}

/// <summary>Mirrors the API's response envelope so tests can assert on it directly.</summary>
public sealed record ApiEnvelope<T>(
    bool Success,
    string? Message,
    T? Data,
    string? Code,
    Dictionary<string, string[]>? Errors);

/// <summary>
/// One SQL Server container for the whole assembly. xUnit runs collections in sequence, which
/// also keeps tests from fighting over the same seeded rows.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
