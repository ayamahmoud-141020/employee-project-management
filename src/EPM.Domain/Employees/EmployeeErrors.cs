using EPM.Domain.Abstractions;

namespace EPM.Domain.Employees;

/// <summary>
/// Every way creating or changing an employee can be refused, in one place.
/// Codes are part of the API contract — rename a message freely, never a code.
/// </summary>
public static class EmployeeErrors
{
    public static readonly Error FirstNameRequired =
        Error.Validation("Employee.FirstNameRequired", "First name is required.");

    public static readonly Error LastNameRequired =
        Error.Validation("Employee.LastNameRequired", "Last name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Employee.NameTooLong", $"Names cannot exceed {Employee.MaxNameLength} characters.");

    public static readonly Error EmailRequired =
        Error.Validation("Employee.EmailRequired", "Email is required.");

    public static readonly Error EmailInvalid =
        Error.Validation("Employee.EmailInvalid", "Email must be a valid email address.");

    public static readonly Error EmailTooLong =
        Error.Validation("Employee.EmailTooLong", $"Email cannot exceed {Email.MaxLength} characters.");

    public static readonly Error PhoneInvalid =
        Error.Validation("Employee.PhoneInvalid", "Phone number is not in a recognised format.");

    public static readonly Error JobTitleRequired =
        Error.Validation("Employee.JobTitleRequired", "Job title is required.");

    public static readonly Error JobTitleTooLong =
        Error.Validation("Employee.JobTitleTooLong", $"Job title cannot exceed {Employee.MaxJobTitleLength} characters.");

    public static readonly Error DepartmentRequired =
        Error.Validation("Employee.DepartmentRequired", "Department is required.");

    public static readonly Error HireDateInFuture =
        Error.Validation("Employee.HireDateInFuture", "Hire date cannot be in the future.");

    public static readonly Error EmailAlreadyExists =
        Error.Conflict("Employee.EmailExists", "Employee email already exists.");

    public static readonly Error AlreadyInactive =
        Error.Conflict("Employee.AlreadyInactive", "Employee is already inactive.");

    public static readonly Error AlreadyActive =
        Error.Conflict("Employee.AlreadyActive", "Employee is already active.");

    public static Error NotFound(int employeeId) =>
        Error.NotFound("Employee.NotFound", $"Employee with id {employeeId} was not found.");
}
