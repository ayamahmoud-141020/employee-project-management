using System.ComponentModel.DataAnnotations;

namespace EPM.Infrastructure.Identity;

/// <summary>
/// Settings for the locally issued JWTs, bound from the "Jwt" configuration section.
/// </summary>
/// <remarks>
/// Validated at startup rather than on first use — a missing signing key should stop the
/// application from booting, not surface as a 500 the first time somebody tries to log in.
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 signing key. The 32-character floor is not arbitrary: a key shorter than
    /// the 256-bit hash output weakens the signature, and the JWT library rejects it outright.
    /// Supplied through user-secrets or an environment variable, never appsettings.json.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters for HMAC-SHA256 signing.")]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Short by design. The access token cannot be revoked once issued, so its lifetime is
    /// the window in which a leaked one stays useful; the refresh token covers the gap.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 60;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 7;
}
