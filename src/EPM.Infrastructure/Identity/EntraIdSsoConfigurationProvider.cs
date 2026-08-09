using EPM.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace EPM.Infrastructure.Identity;

/// <summary>
/// Projects <see cref="EntraIdOptions"/> down to the values a browser client may see.
/// </summary>
/// <remarks>
/// Reports SSO as available only when the options are complete enough for the scheme to have
/// been registered. <c>Enabled</c> alone is not sufficient: a half-filled configuration would
/// otherwise draw a sign-in button that redirects to an authority that does not exist.
/// </remarks>
internal sealed class EntraIdSsoConfigurationProvider(IOptionsMonitor<EntraIdOptions> options)
    : ISsoConfigurationProvider
{
    public SsoConfiguration Current
    {
        get
        {
            var current = options.CurrentValue;

            return current.IsConfigured
                ? new SsoConfiguration(true, current.Authority, current.ClientId, current.EffectiveApiScope)
                : new SsoConfiguration(false, null, null, null);
        }
    }
}
