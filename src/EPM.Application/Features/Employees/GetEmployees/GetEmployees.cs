using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Features.Employees.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Employees.GetEmployees;

/// <summary>
/// The employees list: search, filter, sort and page, all server-side.
/// </summary>
public sealed record GetEmployeesQuery(
    PagingOptions Paging,
    int? DepartmentId,
    bool? IsActive,
    DateOnly? HiredFrom,
    DateOnly? HiredTo) : IRequest<Result<PagedResult<EmployeeResponse>>>;

internal sealed class GetEmployeesHandler(IAppDbContext context)
    : IRequestHandler<GetEmployeesQuery, Result<PagedResult<EmployeeResponse>>>
{
    /// <summary>
    /// Sortable columns. Anything not listed falls back to the default — see SortMap for why
    /// this is a whitelist and not a dynamic property lookup.
    /// </summary>
    private static readonly SortMap<Employee> Sorts = SortMap<Employee>
        .WithDefault("lastName")
        .Add("firstName", employee => employee.FirstName)
        .Add("lastName", employee => employee.LastName)
        .Add("email", employee => employee.Email.Value)
        .Add("jobTitle", employee => employee.JobTitle)
        .Add("department", employee => employee.Department!.Name)
        .Add("hireDate", employee => employee.HireDate)
        .Add("isActive", employee => employee.IsActive);

    public async Task<Result<PagedResult<EmployeeResponse>>> Handle(
        GetEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        var paging = query.Paging.Normalised();

        var employees = context.Employees
            .AsNoTracking()
            // Filters first, then sort, then page — the order the SQL will run in anyway, but
            // written this way so it is obvious the paging applies to the filtered set.
            .WhereIf(query.DepartmentId.HasValue, employee => employee.DepartmentId == query.DepartmentId!.Value)
            .WhereIf(query.IsActive.HasValue, employee => employee.IsActive == query.IsActive!.Value)
            .WhereIf(query.HiredFrom.HasValue, employee => employee.HireDate >= query.HiredFrom!.Value)
            .WhereIf(query.HiredTo.HasValue, employee => employee.HireDate <= query.HiredTo!.Value);

        if (paging.Search is { } search)
        {
            // Contains translates to LIKE '%term%', which cannot use an index — acceptable at
            // this scale and the honest trade for substring search. A table in the millions
            // would want SQL Server full-text search here instead.
            employees = employees.Where(employee =>
                employee.FirstName.Contains(search)
                || employee.LastName.Contains(search)
                || employee.Email.Value.Contains(search)
                || employee.JobTitle.Contains(search));
        }

        var sorted = Sorts.Apply(employees, paging.SortBy, paging.SortDescending)
            // Tie-break on the primary key. Without it, rows with equal sort values can come
            // back in a different order on each query, so an employee can appear on both page
            // 1 and page 2 — or on neither.
            .ThenBy(employee => employee.Id);

        var page = await sorted
            .Select(EmployeeProjections.ToResponse)
            .ToPagedResultAsync(paging, cancellationToken);

        return Result.Success(page);
    }
}
