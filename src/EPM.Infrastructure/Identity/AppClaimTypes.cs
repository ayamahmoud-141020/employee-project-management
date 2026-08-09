namespace EPM.Infrastructure.Identity;

/// <summary>
/// Claim names this application puts into, or reads out of, a token.
/// </summary>
/// <remarks>
/// Short custom names rather than the long schemas.microsoft.com URIs, because every claim
/// name is repeated in full inside every token and the URIs roughly triple the header size.
/// </remarks>
public static class AppClaimTypes
{
    /// <summary>Local <c>Users.Id</c>. Not the same thing as the Entra object id.</summary>
    public const string UserId = "uid";

    /// <summary>Linked <c>Employees.Id</c>, absent when the account has no employee record.</summary>
    public const string EmployeeId = "eid";

    public const string DisplayName = "name";

    /// <summary>
    /// Entra ID's stable per-user identifier. Read during SSO sign-in to match a returning
    /// user; never issued by this application.
    /// </summary>
    public const string ObjectId = "oid";
}
