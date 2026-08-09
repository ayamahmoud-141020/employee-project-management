namespace EPM.Infrastructure.Identity;

/// <summary>
/// Microsoft Entra ID settings, bound from the "EntraId" configuration section.
/// </summary>
/// <remarks>
/// Every property is optional because <see cref="Enabled"/> defaults to false and the whole
/// scheme is skipped when it is. That is what lets the application run, and the test suite
/// pass, on a machine with no tenant and no Azure account.
/// </remarks>
public sealed class EntraIdOptions
{
    public const string SectionName = "EntraId";

    public bool Enabled { get; init; }

    /// <summary>Authority host. Sovereign clouds use a different one, hence the setting.</summary>
    public string Instance { get; init; } = "https://login.microsoftonline.com/";

    /// <summary>Directory (tenant) id from the app registration overview.</summary>
    public string? TenantId { get; init; }

    /// <summary>Application (client) id of the API's app registration.</summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Expected `aud` claim. Usually the same as <see cref="ClientId"/>, but tokens requested
    /// against an Application ID URI carry "api://{clientId}" instead, so it is configurable.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Scope the SPA requests so its access token is issued for this API rather than for
    /// Microsoft Graph. Defaults to the Application ID URI form of <see cref="ClientId"/>,
    /// which is what "Expose an API" creates unless the URI was customised.
    /// </summary>
    public string? ApiScope { get; init; }

    /// <summary>
    /// Authority the browser is redirected to. Single-tenant, because a multi-tenant authority
    /// would let any Microsoft directory's users reach the sign-in page.
    /// </summary>
    public string? Authority =>
        IsConfigured ? $"{Instance.TrimEnd('/')}/{TenantId}" : null;

    /// <summary>
    /// <see cref="ApiScope"/> if set, otherwise the conventional Application ID URI scope.
    /// </summary>
    public string? EffectiveApiScope =>
        !string.IsNullOrWhiteSpace(ApiScope) ? ApiScope
        : IsConfigured ? $"api://{ClientId}/access_as_user"
        : null;

    /// <summary>
    /// Role granted to a user signing in through SSO for the first time who carries no
    /// recognised app role. Deliberately the least privileged one — an unmapped identity
    /// should be able to look around, not administer anything.
    /// </summary>
    /// <remarks>
    /// This applies to a delegated token that carries a scope but no recognised `roles` entry —
    /// a signed-in user who was never assigned an app role. It cannot rescue a token carrying
    /// neither `scp` nor `roles`: Microsoft.Identity.Web rejects those with a 401 before any
    /// application code runs. Verified by
    /// <c>EntraIdSsoTests.A_token_with_neither_scope_nor_role_is_rejected</c>.
    /// </remarks>
    public string DefaultRole { get; init; } = Domain.Identity.RoleNames.User;

    /// <summary>
    /// True once the settings are complete enough to actually register the scheme. Checked
    /// separately from <see cref="Enabled"/> so a half-filled config fails loudly at startup
    /// instead of producing a scheme that rejects every token.
    /// </summary>
    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);
}
