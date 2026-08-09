using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Features.Projects.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Projects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Projects.GetProjects;

public sealed record GetProjectsQuery(
    PagingOptions Paging,
    ProjectStatus? Status,
    DateOnly? StartsFrom,
    DateOnly? StartsTo,
    int? EmployeeId) : IRequest<Result<PagedResult<ProjectResponse>>>;

internal sealed class GetProjectsHandler(IAppDbContext context)
    : IRequestHandler<GetProjectsQuery, Result<PagedResult<ProjectResponse>>>
{
    private static readonly SortMap<Project> Sorts = SortMap<Project>
        .WithDefault("startDate")
        .Add("name", project => project.Name)
        .Add("startDate", project => project.Schedule.Start)
        .Add("endDate", project => project.Schedule.End)
        .Add("status", project => project.Status)
        .Add("assignedEmployeeCount", project => project.Assignments.Count);

    public async Task<Result<PagedResult<ProjectResponse>>> Handle(
        GetProjectsQuery query,
        CancellationToken cancellationToken)
    {
        var paging = query.Paging.Normalised();

        var projects = context.Projects
            .AsNoTracking()
            .WhereIf(query.Status.HasValue, project => project.Status == query.Status!.Value)
            .WhereIf(query.StartsFrom.HasValue, project => project.Schedule.Start >= query.StartsFrom!.Value)
            .WhereIf(query.StartsTo.HasValue, project => project.Schedule.Start <= query.StartsTo!.Value)
            // "Projects this person is on" — powers the employee detail view and lets a User
            // filter the project list down to their own work.
            .WhereIf(
                query.EmployeeId.HasValue,
                project => project.Assignments.Any(assignment => assignment.EmployeeId == query.EmployeeId!.Value));

        if (paging.Search is { } search)
        {
            projects = projects.Where(project =>
                project.Name.Contains(search)
                || (project.Description != null && project.Description.Contains(search)));
        }

        var sorted = Sorts.Apply(projects, paging.SortBy, paging.SortDescending)
            .ThenBy(project => project.Id);

        var page = await sorted
            .Select(ProjectProjections.ToResponse)
            .ToPagedResultAsync(paging, cancellationToken);

        return Result.Success(page);
    }
}
