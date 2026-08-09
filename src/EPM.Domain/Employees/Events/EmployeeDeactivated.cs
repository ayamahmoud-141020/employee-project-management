using EPM.Domain.Abstractions;

namespace EPM.Domain.Employees.Events;

/// <summary>
/// Raised when an employee is deactivated. Handled by unassigning them from every project
/// they are still on — otherwise a deactivated person keeps holding allocation capacity that
/// nobody can see or reclaim.
/// </summary>
public sealed record EmployeeDeactivated(int EmployeeId) : IDomainEvent;
