namespace EPM.Application.Features.Departments.Contracts;

/// <summary>
/// A department as the API reports it.
/// </summary>
/// <remarks>
/// The employee counts are here because every screen that shows a department shows them —
/// the list, the detail view, and the delete confirmation, which needs to explain why a
/// department with people in it cannot be removed. Computing them in the projection costs one
/// join; fetching them separately would cost a round trip per row.
/// </remarks>
public sealed record DepartmentResponse(
    int Id,
    string Name,
    string? Description,
    int EmployeeCount,
    int ActiveEmployeeCount);
