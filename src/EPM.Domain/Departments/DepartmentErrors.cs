using EPM.Domain.Abstractions;

namespace EPM.Domain.Departments;

public static class DepartmentErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Department.NameRequired", "Department name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Department.NameTooLong", $"Department name cannot exceed {Department.MaxNameLength} characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("Department.DescriptionTooLong", $"Description cannot exceed {Department.MaxDescriptionLength} characters.");

    public static readonly Error NameAlreadyExists =
        Error.Conflict("Department.NameExists", "A department with this name already exists.");

    public static Error HasActiveEmployees(int activeCount) =>
        Error.Conflict(
            "Department.HasActiveEmployees",
            $"This department cannot be deleted while {activeCount} active employee(s) belong to it.");

    public static Error NotFound(int departmentId) =>
        Error.NotFound("Department.NotFound", $"Department with id {departmentId} was not found.");
}
