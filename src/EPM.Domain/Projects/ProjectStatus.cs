namespace EPM.Domain.Projects;

/// <summary>
/// Where a project is in its lifecycle.
/// </summary>
/// <remarks>
/// Values are pinned explicitly because they are persisted as integers. Reordering or
/// inserting a member without a new number would silently reinterpret existing rows.
/// </remarks>
public enum ProjectStatus
{
    Planning = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4,
}
