using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace EPM.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A standards-compliant OpenID Connect issuer, running locally.
/// </summary>
/// <remarks>
/// This is what makes the Entra ID path testable without an Azure tenant. It serves the two
/// documents any OIDC relying party actually consumes — the discovery document and a JWKS —
/// and signs tokens with an RSA key whose public half is published in that JWKS.
///
/// The point is that nothing is stubbed on the application side. Microsoft.Identity.Web
/// performs its real discovery fetch, downloads real signing keys, and validates a real
/// signature, issuer, audience and expiry. What is fake is the *directory*, not the protocol.
///
/// A real Azure tenant would additionally exercise Microsoft's own token issuance and the
/// portal configuration — neither of which is code in this repository.
/// </remarks>
public sealed class FakeEntraIdServer : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly RsaSecurityKey _signingKey;

    public FakeEntraIdServer(string tenantId)
    {
        TenantId = tenantId;

        // A fresh key per run, so a leaked test key is worth nothing.
        var rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(rsa) { KeyId = Guid.NewGuid().ToString("N") };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); // port 0 → the OS picks a free one
        builder.Logging.ClearProviders();

        var app = builder.Build();

        // Microsoft.Identity.Web derives the authority as {Instance}{TenantId}/v2.0 and asks
        // for its discovery document. Both the v2.0-suffixed and bare forms are served, because
        // which one is requested depends on how the authority is composed.
        app.MapGet("/{tenant}/v2.0/.well-known/openid-configuration", (string tenant) => Discovery(tenant));
        app.MapGet("/{tenant}/.well-known/openid-configuration", (string tenant) => Discovery(tenant));
        app.MapGet("/{tenant}/discovery/v2.0/keys", () => Jwks());

        _host = app;
        _host.Start();

        BaseAddress = _host.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First()
            .TrimEnd('/');
    }

    public string TenantId { get; }

    /// <summary>Root URL, e.g. http://127.0.0.1:41234 — used as the EntraId:Instance value.</summary>
    public string BaseAddress { get; }

    /// <summary>The `iss` value this server stamps on its tokens and advertises in discovery.</summary>
    public string Issuer => $"{BaseAddress}/{TenantId}/v2.0";

    /// <summary>
    /// Issues a signed access token shaped like one from Entra ID.
    /// </summary>
    /// <param name="appRoles">
    /// Values of the `roles` claim, matching the app roles declared in the app registration
    /// (EPM.Admin / EPM.Manager / EPM.User). Pass none to model a user with no role assigned.
    /// </param>
    /// <param name="scope">
    /// The `scp` claim. Present on every delegated (user) token, because the client requests a
    /// scope during the auth-code flow. Pass null to model a token carrying neither scope nor
    /// role — which Microsoft.Identity.Web rejects outright, see
    /// <c>A_token_with_neither_scope_nor_role_is_rejected</c>.
    /// </param>
    public string IssueToken(
        string audience,
        string objectId,
        string? email = null,
        string? name = null,
        IEnumerable<string>? appRoles = null,
        DateTime? expiresAt = null,
        string? scope = "access_as_user")
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, objectId),
            new("oid", objectId),
            new("tid", TenantId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        if (email is not null)
        {
            claims.Add(new Claim("preferred_username", email));
        }

        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        if (scope is not null)
        {
            claims.Add(new Claim("scp", scope));
        }

        // Entra sends one `roles` entry per assigned app role.
        foreach (var role in appRoles ?? [])
        {
            claims.Add(new Claim("roles", role));
        }

        var expiry = expiresAt ?? DateTime.UtcNow.AddMinutes(30);

        // Anchored to the expiry rather than to "now", so that deliberately expired tokens are
        // still internally consistent — the handler refuses to build one whose notBefore falls
        // after its expiry, which would fail the test for the wrong reason.
        var notBefore = expiry.AddMinutes(-31);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore,
            expires: expiry,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Issues a correctly signed token that claims a different issuer, to prove the issuer
    /// check is live rather than nominally configured.
    /// </summary>
    public string IssueTokenWithForeignIssuer(string audience, string objectId)
    {
        var token = new JwtSecurityToken(
            issuer: "https://login.microsoftonline.com/some-other-tenant/v2.0",
            audience: audience,
            claims: [new Claim("oid", objectId), new Claim("roles", "EPM.Admin")],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Signs a token with a *different* key than the one published in the JWKS, to prove the
    /// API actually verifies signatures rather than merely parsing them.
    /// </summary>
    public string IssueTokenSignedWithUntrustedKey(string audience, string objectId)
    {
        var rogue = new RsaSecurityKey(RSA.Create(2048)) { KeyId = _signingKey.KeyId };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims: [new Claim("oid", objectId), new Claim("roles", "EPM.Admin")],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(rogue, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private object Discovery(string tenant) => new
    {
        issuer = $"{BaseAddress}/{tenant}/v2.0",
        jwks_uri = $"{BaseAddress}/{tenant}/discovery/v2.0/keys",
        authorization_endpoint = $"{BaseAddress}/{tenant}/oauth2/v2.0/authorize",
        token_endpoint = $"{BaseAddress}/{tenant}/oauth2/v2.0/token",
        response_types_supported = new[] { "code", "id_token" },
        subject_types_supported = new[] { "pairwise" },
        id_token_signing_alg_values_supported = new[] { "RS256" },
    };

    private object Jwks()
    {
        var parameters = _signingKey.Rsa.ExportParameters(includePrivateParameters: false);

        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = _signingKey.KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent),
                },
            },
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
