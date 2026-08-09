using EPM.Application.Abstractions;
using EPM.Application.Features.Departments.Contracts;
using EPM.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Departments.GetDepartments;

/// <summary>
/// Every department, with employee counts.
/// </summary>
/// <remarks>
/// Not paged, unlike employees and projects. Departments are an organisational structure —
/// there are tens of them, not thousands — and the employee form needs the whole list to
/// populate its dropdown. Paging here would mean the dropdown silently missing options.
/// </remarks>
public sealed record GetDepartmentsQuery(string? Search) : IRequest<Result<IReadOnlyList<DepartmentResponse>>>;

internal sealed class GetDepartmentsHandler(IAppDbContext context)
    : IRequestHandler<GetDepartmentsQuery, Result<IReadOnlyList<DepartmentResponse>>>
{
    public async Task<Result<IReadOnlyList<DepartmentResponse>>> Handle(
        GetDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        var search = query.Search?.Trim();

        var departments = await context.Departments
            .AsNoTracking()
            .Where(department => search == null
                                 || department.Name.Contains(search)
                                 || (department.Description != null && department.Description.Contains(search)))
            .OrderBy(department => department.Name)
            .Select(DepartmentProjections.ToResponse(context.Employees))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<DepartmentResponse>>(departments);
    }
}
