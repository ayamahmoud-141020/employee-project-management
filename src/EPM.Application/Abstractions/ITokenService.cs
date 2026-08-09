using EPM.Domain.Identity;

namespace EPM.Application.Abstractions;

/// <summary>An issued access token and the moment it stops being accepted.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

/// <summary>
/// Mints the tokens the API accepts. Only ever issues locally signed tokens — tokens from
/// Entra ID are validated, never created, by this application.
/// </summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(AppUser user);

    /// <summary>
    /// A cryptographically random opaque string. Not a JWT: a refresh token carries no
    /// claims, it is only ever looked up in the database, so signing it would add nothing.
    /// </summary>
    string CreateRefreshToken();

    DateTime GetRefreshTokenExpiry(DateTime issuedAtUtc);
}
