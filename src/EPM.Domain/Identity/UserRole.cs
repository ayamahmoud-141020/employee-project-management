namespace EPM.Domain.Identity;

/// <summary>
/// What a signed-in account is allowed to do. Persisted as an integer, so the numbers are
/// pinned — see <see cref="Projects.ProjectStatus"/> for the same reasoning.
/// </summary>
/// <remarks>
/// A single role per user, not a collection. The spec describes three tiers where each one
/// is strictly broader than the last, and modelling that as many-to-many would add a table
/// and a join for no behaviour we actually need. If overlapping roles ever appear, this
/// becomes a UserRoles collection and only the token service and policies change.
/// </remarks>
public enum UserRole
{
    /// <summary>Read-only access, plus their own project assignments.</summary>
    User = 1,

    /// <summary>Everything a User can do, plus projects and assignments.</summary>
    Manager = 2,

    /// <summary>Full access, including employees and departments.</summary>
    Admin = 3,
}

/// <summary>
/// Role names as they appear in the JWT `role` claim and in [Authorize] policies.
/// String constants rather than enum.ToString() so a rename of the enum member cannot
/// silently invalidate every issued token.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";

    public static string From(UserRole role) => role switch
    {
        UserRole.Admin => Admin,
        UserRole.Manager => Manager,
        UserRole.User => User,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unmapped role."),
    };
}
