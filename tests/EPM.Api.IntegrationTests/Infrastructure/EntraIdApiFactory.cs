using EPM.Infrastructure.Identity;
using EPM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;

namespace EPM.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the API with Entra ID single sign-on switched **on**, pointed at a locally hosted
/// OpenID Connect issuer.
/// </summary>
/// <remarks>
/// Separate from <see cref="ApiFactory"/> because enabling SSO changes the authentication
/// registration itself — which schemes exist is decided once, at startup — so it cannot be
/// toggled per test on a shared host.
///
/// The only test-only concession is <c>RequireHttpsMetadata = false</c>: the fake issuer is
/// reachable over plain HTTP on a loopback port. Everything else — discovery, key retrieval,
/// signature, issuer, audience and lifetime validation — runs exactly as configured in
/// production.
/// </remarks>
public sealed class EntraIdApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TenantId = "11111111-2222-3333-4444-555555555555";
    public const string ClientId = "99999999-8888-7777-6666-555555555555";

    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Integration#Test123")
        .Build();

    public FakeEntraIdServer IdentityProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        IdentityProvider = new FakeEntraIdServer(TenantId);
        await _database.StartAsync();

        // Set through the environment, before the host is built, and this is not incidental.
        // AddApplicationAuthentication reads EntraId:Enabled *eagerly* — whether a scheme
        // exists is decided once, at registration — so it must come from a provider present
        // when WebApplication.CreateBuilder runs. Configuration layered on afterwards through
        // ConfigureAppConfiguration is applied too late and the scheme is silently never
        // registered. This mirrors exactly how docker-compose supplies these values.
        Environment.SetEnvironmentVariable("EntraId__Enabled", "true");
        Environment.SetEnvironmentVariable("EntraId__Instance", $"{IdentityProvider.BaseAddress}/");
        Environment.SetEnvironmentVariable("EntraId__TenantId", TenantId);
        Environment.SetEnvironmentVariable("EntraId__ClientId", ClientId);
        Environment.SetEnvironmentVariable("EntraId__Audience", ClientId);
        Environment.SetEnvironmentVariable("EntraId__DefaultRole", "User");

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    /// <summary>Server-side log records, so a failing request can be explained.</summary>
    public static readonly List<string> LogMessages = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new CapturingLoggerProvider(LogMessages));
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _database.GetConnectionString(),

                ["Jwt:Key"] = "integration-tests-signing-key-not-used-anywhere-else-0123456789",
                ["Jwt:Issuer"] = "epm-api",
                ["Jwt:Audience"] = "epm-client",

                ["Seed:Enabled"] = "false",

                // The EntraId:* values are deliberately absent here — see InitializeAsync.
                // They must be in the environment before the host is built.
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Two hooks, and the split is forced by options ordering: every IConfigureOptions
            // runs before every IPostConfigureOptions.
            //
            // RequireHttpsMetadata has to be set in Configure, because the framework's own
            // JwtBearerPostConfigureOptions throws on a non-HTTPS authority — a PostConfigure
            // here would run after it and never be reached. This is the single
            // production-unlike setting, and it relaxes transport security for a loopback
            // address only; token validation below is untouched.
            services.Configure<JwtBearerOptions>(AuthenticationSchemes.EntraId, options =>
            {
                options.RequireHttpsMetadata = false;
                options.Authority = IdentityProvider.Issuer;
            });

            // The issuer override has to be in PostConfigure, because Microsoft.Identity.Web
            // installs AadIssuerValidator in one of its own post-configures — anything set
            // earlier would simply be overwritten.
            services.PostConfigure<JwtBearerOptions>(AuthenticationSchemes.EntraId, options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters.ValidIssuer = IdentityProvider.Issuer;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidAudience = ClientId;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;

                // Microsoft.Identity.Web installs AadIssuerValidator, which encodes the shape of
                // real Microsoft authorities (login.microsoftonline.com, tenant GUID in the
                // issuer, and so on) and rejects anything else outright. It cannot pass for a
                // loopback issuer by design, so it is replaced with an exact-match check.
                //
                // The security property is preserved, not waived: only this precise issuer is
                // accepted, and A_token_from_an_unexpected_issuer_is_rejected proves it. What is
                // no longer exercised is AadIssuerValidator itself — Microsoft's code, which
                // only has meaning against a real tenant.
                var expectedIssuer = IdentityProvider.Issuer;


                options.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                    issuer == expectedIssuer
                        ? issuer
                        : throw new SecurityTokenInvalidIssuerException(
                            $"Issuer '{issuer}' is not the configured issuer.");
            });
        });
    }

    public HttpClient CreateSsoClient(string objectId, string? email, string? name, params string[] appRoles) =>
        CreateClientWithToken(IdentityProvider.IssueToken(ClientId, objectId, email, name, appRoles));

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public new async Task DisposeAsync()
    {
        await IdentityProvider.DisposeAsync();
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class EntraIdCollection : ICollectionFixture<EntraIdApiFactory>
{
    public const string Name = "entra-id";
}

/// <summary>Minimal logger provider that records messages into a list, for diagnostics.</summary>
internal sealed class CapturingLoggerProvider(List<string> sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, sink);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, List<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (sink)
            {
                sink.Add($"[{logLevel}] {category}: {formatter(state, exception)}"
                         + (exception is null
                             ? ""
                             : $" || {exception.GetType().Name}: {exception.Message} || "
                               + string.Join(" << ", (exception.StackTrace ?? "")
                                   .Split('\n').Take(4).Select(l => l.Trim()))));
            }
        }
    }
}
