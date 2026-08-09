using System.Linq.Expressions;
using EPM.Domain.Employees;

namespace EPM.Application.Features.Employees.Contracts;

public sealed record EmployeeResponse(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    string JobTitle,
    int DepartmentId,
    string DepartmentName,
    DateOnly HireDate,
    bool IsActive);

internal static class EmployeeProjections
{
    /// <summary>
    /// Employee to response, including the department name.
    /// </summary>
    /// <remarks>
    /// A projection rather than Include(e => e.Department): this produces a SELECT of exactly
    /// the eleven columns below with one join, instead of pulling two whole entity graphs back
    /// and discarding most of them. On a paged list of 20 that difference is small; on the
    /// dashboard and on a 200-row page it is not.
    ///
    /// Department is a nullable navigation, so the name is coalesced — a row whose department
    /// vanished should not blow up the whole list with a null-reference.
    /// </remarks>
    public static readonly Expression<Func<Employee, EmployeeResponse>> ToResponse =
        employee => new EmployeeResponse(
            employee.Id,
            employee.FirstName,
            employee.LastName,
            employee.FirstName + " " + employee.LastName,
            employee.Email.Value,
            employee.Phone != null ? employee.Phone.Value : null,
            employee.JobTitle,
            employee.DepartmentId,
            employee.Department != null ? employee.Department.Name : string.Empty,
            employee.HireDate,
            employee.IsActive);
}
