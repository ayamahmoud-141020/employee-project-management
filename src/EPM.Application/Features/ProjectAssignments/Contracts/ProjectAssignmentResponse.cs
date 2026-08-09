namespace EPM.Application.Features.ProjectAssignments.Contracts;

/// <summary>One member of a project team, as returned by GET /api/projects/{id}/employees.</summary>
public sealed record ProjectAssignmentResponse(
    int Id,
    int EmployeeId,
    string EmployeeName,
    string EmployeeEmail,
    string DepartmentName,
    bool EmployeeIsActive,
    string Role,
    DateOnly AssignedDate,
    int AllocationPercentage);
