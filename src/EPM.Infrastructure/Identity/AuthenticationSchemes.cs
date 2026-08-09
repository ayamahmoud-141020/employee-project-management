namespace EPM.Infrastructure.Identity;

public static class AuthenticationSchemes
{
    /// <summary>
    /// The scheme endpoints actually name. It authenticates nothing itself — it looks at the
    /// incoming token and forwards to whichever real scheme issued it.
    /// </summary>
    public const string Smart = "Smart";

    /// <summary>Tokens this API signed itself, from the username/password login.</summary>
    public const string Local = "Local";

    /// <summary>Tokens signed by Microsoft Entra ID. Only registered when SSO is configured.</summary>
    public const string EntraId = "EntraId";
}
