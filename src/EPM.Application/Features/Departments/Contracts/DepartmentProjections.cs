using System.Linq.Expressions;
using EPM.Domain.Departments;

namespace EPM.Application.Features.Departments.Contracts;

internal static class DepartmentProjections
{
    /// <summary>
    /// Department to response, as an expression so EF translates it into the SELECT rather
    /// than materialising entities and mapping them in memory.
    /// </summary>
    /// <remarks>
    /// Shared by the list and detail slices. Read projections are the one thing worth
    /// centralising across slices in this codebase — duplicating a subquery is how the list
    /// and the detail view end up disagreeing about a count.
    /// </remarks>
    public static Expression<Func<Department, DepartmentResponse>> ToResponse(IQueryable<Domain.Employees.Employee> employees) =>
        department => new DepartmentResponse(
            department.Id,
            department.Name,
            department.Description,
            employees.Count(employee => employee.DepartmentId == department.Id),
            employees.Count(employee => employee.DepartmentId == department.Id && employee.IsActive));
}
