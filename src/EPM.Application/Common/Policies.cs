using EPM.Domain.Identity;

namespace EPM.Application.Common;

/// <summary>
/// Authorization policy names, shared between the endpoints that require them and the
/// startup code that defines them.
/// </summary>
/// <remarks>
/// Endpoints ask for a capability ("can manage projects"), not a role ("Manager"). When the
/// role matrix changes — say Managers gain department access — it changes in one place here
/// instead of across every endpoint that happened to list the roles inline.
/// </remarks>
public static class Policies
{
    public const string CanManageEmployees = nameof(CanManageEmployees);
    public const string CanManageDepartments = nameof(CanManageDepartments);
    public const string CanManageProjects = nameof(CanManageProjects);
    public const string CanManageAssignments = nameof(CanManageAssignments);
    public const string CanViewDirectory = nameof(CanViewDirectory);

    /// <summary>
    /// Which roles satisfy each policy. Read by startup to register the policies and by the
    /// Swagger description so the docs cannot drift from the actual rules.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> RolesByPolicy =
        new Dictionary<string, string[]>
        {
            [CanManageEmployees] = [RoleNames.Admin],
            [CanManageDepartments] = [RoleNames.Admin],
            [CanManageProjects] = [RoleNames.Admin, RoleNames.Manager],
            [CanManageAssignments] = [RoleNames.Admin, RoleNames.Manager],
            [CanViewDirectory] = [RoleNames.Admin, RoleNames.Manager, RoleNames.User],
        };
}
