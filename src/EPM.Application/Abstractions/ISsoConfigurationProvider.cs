namespace EPM.Application.Abstractions;

/// <summary>
/// The parts of the Entra ID configuration a browser client needs in order to sign in.
/// </summary>
/// <remarks>
/// None of these are secrets — a public client sends all three in the query string of its
/// authorization request, so they are visible to anyone who opens the network tab. What must
/// never appear here is a client secret, which is why this is a projection of
/// <c>EntraIdOptions</c> rather than the options object itself.
/// </remarks>
public sealed record SsoConfiguration(
    bool Enabled,
    string? Authority,
    string? ClientId,
    string? ApiScope);

/// <summary>
/// Supplies the browser-facing half of the SSO configuration.
/// </summary>
/// <remarks>
/// An abstraction rather than a direct read of <c>EntraIdOptions</c> because those live in
/// Infrastructure, and Application does not reference Infrastructure.
/// </remarks>
public interface ISsoConfigurationProvider
{
    SsoConfiguration Current { get; }
}
