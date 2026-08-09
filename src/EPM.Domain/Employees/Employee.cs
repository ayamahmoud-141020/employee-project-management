using EPM.Domain.Abstractions;
using EPM.Domain.Departments;
using EPM.Domain.Employees.Events;

namespace EPM.Domain.Employees;

/// <summary>
/// A person on the payroll. Aggregate root: everything that can make an employee record
/// invalid is checked here, so no caller can construct one in a broken state.
/// </summary>
public sealed class Employee : AggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxJobTitleLength = 150;

    // EF materialiser. Private so application code has to go through Create().
    private Employee()
    {
    }

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public Email Email { get; private set; } = null!;

    public PhoneNumber? Phone { get; private set; }

    public string JobTitle { get; private set; } = null!;

    public int DepartmentId { get; private set; }

    /// <summary>
    /// Navigation for read-side Includes only. The employee aggregate owns the id, not the
    /// department itself — a department is a separate aggregate with its own lifecycle.
    /// </summary>
    public Department? Department { get; private set; }

    public DateOnly HireDate { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime? DeactivatedAtUtc { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Creates an employee, enforcing every rule from the spec that can be checked without
    /// hitting the database. Email uniqueness is not among them: that spans the whole table,
    /// so it belongs to the unique index plus the create/update handlers.
    /// </summary>
    /// <param name="today">
    /// Supplied by the caller rather than read from DateTime.Today. The domain staying off
    /// the system clock is what makes "hire date cannot be in the future" testable without
    /// freezing time globally, and it keeps the rule honest across time zones — the caller
    /// decides whose "today" applies.
    /// </param>
    public static Result<Employee> Create(
        string? firstName,
        string? lastName,
        string? email,
        string? phone,
        string? jobTitle,
        int departmentId,
        DateOnly hireDate,
        DateOnly today)
    {
        var names = ValidateNames(firstName, lastName);

        if (names.IsFailure)
        {
            return Result.Failure<Employee>(names.Error);
        }

        var jobTitleResult = ValidateJobTitle(jobTitle);

        if (jobTitleResult.IsFailure)
        {
            return Result.Failure<Employee>(jobTitleResult.Error);
        }

        var emailResult = Email.Create(email);

        if (emailResult.IsFailure)
        {
            return Result.Failure<Employee>(emailResult.Error);
        }

        var phoneResult = PhoneNumber.CreateOptional(phone);

        if (phoneResult.IsFailure)
        {
            return Result.Failure<Employee>(phoneResult.Error);
        }

        if (departmentId <= 0)
        {
            return Result.Failure<Employee>(EmployeeErrors.DepartmentRequired);
        }

        if (hireDate > today)
        {
            return Result.Failure<Employee>(EmployeeErrors.HireDateInFuture);
        }

        return new Employee
        {
            FirstName = names.Value.First,
            LastName = names.Value.Last,
            Email = emailResult.Value,
            Phone = phoneResult.Value,
            JobTitle = jobTitleResult.Value,
            DepartmentId = departmentId,
            HireDate = hireDate,
            IsActive = true,
        };
    }

    /// <summary>
    /// Applies an edit. Takes the whole set of editable fields rather than exposing one
    /// setter per field, so the object is only ever re-validated as a complete unit.
    /// </summary>
    public Result Update(
        string? firstName,
        string? lastName,
        string? email,
        string? phone,
        string? jobTitle,
        int departmentId,
        DateOnly hireDate,
        DateOnly today)
    {
        var names = ValidateNames(firstName, lastName);

        if (names.IsFailure)
        {
            return Result.Failure(names.Error);
        }

        var jobTitleResult = ValidateJobTitle(jobTitle);

        if (jobTitleResult.IsFailure)
        {
            return Result.Failure(jobTitleResult.Error);
        }

        var emailResult = Email.Create(email);

        if (emailResult.IsFailure)
        {
            return Result.Failure(emailResult.Error);
        }

        var phoneResult = PhoneNumber.CreateOptional(phone);

        if (phoneResult.IsFailure)
        {
            return Result.Failure(phoneResult.Error);
        }

        if (departmentId <= 0)
        {
            return Result.Failure(EmployeeErrors.DepartmentRequired);
        }

        if (hireDate > today)
        {
            return Result.Failure(EmployeeErrors.HireDateInFuture);
        }

        FirstName = names.Value.First;
        LastName = names.Value.Last;
        Email = emailResult.Value;
        Phone = phoneResult.Value;
        JobTitle = jobTitleResult.Value;
        DepartmentId = departmentId;
        HireDate = hireDate;

        return Result.Success();
    }

    /// <summary>
    /// Soft delete. The spec prefers deactivation over removal, so the row stays and history
    /// (past project assignments, reporting) survives.
    /// </summary>
    public Result Deactivate(DateTime utcNow)
    {
        if (!IsActive)
        {
            return Result.Failure(EmployeeErrors.AlreadyInactive);
        }

        IsActive = false;
        DeactivatedAtUtc = utcNow;

        // Someone has to unwind the open project allocations; that is a different aggregate,
        // so it happens in a handler for this event rather than inline here.
        Raise(new EmployeeDeactivated(Id));

        return Result.Success();
    }

    public Result Reactivate()
    {
        if (IsActive)
        {
            return Result.Failure(EmployeeErrors.AlreadyActive);
        }

        IsActive = true;
        DeactivatedAtUtc = null;

        return Result.Success();
    }

    private static Result<(string First, string Last)> ValidateNames(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Result.Failure<(string, string)>(EmployeeErrors.FirstNameRequired);
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Result.Failure<(string, string)>(EmployeeErrors.LastNameRequired);
        }

        var first = firstName.Trim();
        var last = lastName.Trim();

        if (first.Length > MaxNameLength || last.Length > MaxNameLength)
        {
            return Result.Failure<(string, string)>(EmployeeErrors.NameTooLong);
        }

        return (first, last);
    }

    private static Result<string> ValidateJobTitle(string? jobTitle)
    {
        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            return Result.Failure<string>(EmployeeErrors.JobTitleRequired);
        }

        var trimmed = jobTitle.Trim();

        return trimmed.Length > MaxJobTitleLength
            ? Result.Failure<string>(EmployeeErrors.JobTitleTooLong)
            : trimmed;
    }
}
