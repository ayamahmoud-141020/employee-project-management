using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Domain.Abstractions;
using EPM.Domain.Projects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Dashboard.GetDashboard;

public sealed record DashboardResponse(
    int TotalEmployees,
    int ActiveEmployees,
    int InactiveEmployees,
    int TotalDepartments,
    int TotalProjects,
    int ActiveProjects,
    IReadOnlyList<DepartmentHeadcount> EmployeesByDepartment,
    IReadOnlyList<ProjectStatusCount> ProjectsByStatus);

public sealed record DepartmentHeadcount(int DepartmentId, string DepartmentName, int EmployeeCount, int ActiveEmployeeCount);

public sealed record ProjectStatusCount(ProjectStatus Status, int Count);

public sealed record GetDashboardQuery : IRequest<Result<DashboardResponse>>;

/// <summary>
/// The dashboard tiles and both optional breakdowns.
/// </summary>
/// <remarks>
/// Four aggregate queries and two grouped ones, all executed in SQL — nothing is counted in
/// memory. The obvious alternative, loading the tables and using LINQ-to-objects, reads the
/// same but transfers every row in the database to render six numbers.
///
/// The queries run sequentially rather than with Task.WhenAll: a DbContext is not thread-safe
/// and concurrent queries on one instance throw. Six round trips on an indexed count is not
/// the bottleneck worth introducing a second context to fix.
/// </remarks>
internal sealed class GetDashboardHandler(IAppDbContext context)
    : IRequestHandler<GetDashboardQuery, Result<DashboardResponse>>
{
    public async Task<Result<DashboardResponse>> Handle(
        GetDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var totalEmployees = await context.Employees.CountAsync(cancellationToken);
        var activeEmployees = await context.Employees.CountAsync(employee => employee.IsActive, cancellationToken);
        var totalDepartments = await context.Departments.CountAsync(cancellationToken);
        var totalProjects = await context.Projects.CountAsync(cancellationToken);

        var activeProjects = await context.Projects
            .CountAsync(project => project.Status == ProjectStatus.Active, cancellationToken);

        // Grouped from the Departments side, not the Employees side, so a department with
        // nobody in it still shows up as a zero instead of vanishing from the chart.
        var employeesByDepartment = await context.Departments
            .AsNoTracking()
            .OrderBy(department => department.Name)
            .Select(department => new DepartmentHeadcount(
                department.Id,
                department.Name,
                context.Employees.Count(employee => employee.DepartmentId == department.Id),
                context.Employees.Count(employee => employee.DepartmentId == department.Id && employee.IsActive)))
            .ToListAsync(cancellationToken);

        var countsByStatus = await context.Projects
            .AsNoTracking()
            .GroupBy(project => project.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Status, entry => entry.Count, cancellationToken);

        // Every status is listed, including the ones with no projects. A chart that drops its
        // empty categories changes shape as data arrives, which makes it hard to read.
        var projectsByStatus = Enum.GetValues<ProjectStatus>()
            .Select(status => new ProjectStatusCount(status, countsByStatus.GetValueOrDefault(status)))
            .ToList();

        return new DashboardResponse(
            totalEmployees,
            activeEmployees,
            totalEmployees - activeEmployees,
            totalDepartments,
            totalProjects,
            activeProjects,
            employeesByDepartment,
            projectsByStatus);
    }
}

internal sealed class GetDashboardEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("dashboard", async (ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetDashboardQuery(), ct);

                return result.ToHttpResult();
            })
            .WithName("GetDashboard")
            .WithTags("Dashboard")
            .WithSummary("Headline counts plus breakdowns by department and project status")
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<DashboardResponse>>();
    }
}
