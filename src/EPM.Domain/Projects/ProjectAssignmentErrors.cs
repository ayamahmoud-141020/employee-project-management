using EPM.Domain.Abstractions;

namespace EPM.Domain.Projects;

public static class ProjectAssignmentErrors
{
    public static readonly Error RoleRequired =
        Error.Validation("Assignment.RoleRequired", "A role is required for the assignment.");

    public static readonly Error RoleTooLong =
        Error.Validation("Assignment.RoleTooLong", $"Role cannot exceed {ProjectAssignment.MaxRoleLength} characters.");

    public static readonly Error AllocationOutOfRange =
        Error.Validation(
            "Assignment.AllocationOutOfRange",
            $"Allocation percentage must be between {Allocation.Minimum} and {Allocation.Maximum}.");

    public static readonly Error EmployeeInactive =
        Error.Conflict("Assignment.EmployeeInactive", "Inactive employees cannot be assigned to projects.");

    public static readonly Error DuplicateAssignment =
        Error.Conflict("Assignment.Duplicate", "This employee is already assigned to the project.");

    public static Error AssignedDateOutsideProjectSchedule(DateRange schedule) =>
        Error.Validation(
            "Assignment.DateOutsideProjectSchedule",
            schedule.End.HasValue
                ? $"Assigned date must fall between {schedule.Start:yyyy-MM-dd} and {schedule.End:yyyy-MM-dd}."
                : $"Assigned date cannot be earlier than the project start date ({schedule.Start:yyyy-MM-dd}).");

    public static Error NotAssigned(int employeeId) =>
        Error.NotFound("Assignment.NotFound", $"Employee {employeeId} is not assigned to this project.");
}
