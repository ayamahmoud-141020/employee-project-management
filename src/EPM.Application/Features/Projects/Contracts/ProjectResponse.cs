using System.Linq.Expressions;
using EPM.Domain.Projects;

namespace EPM.Application.Features.Projects.Contracts;

public sealed record ProjectResponse(
    int Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly? EndDate,
    ProjectStatus Status,
    int AssignedEmployeeCount);

internal static class ProjectProjections
{
    /// <remarks>
    /// The assignment count comes from a correlated subquery rather than
    /// Include(p => p.Assignments).Count(): the list only needs the number, and Include would
    /// fetch every assignment row for every project on the page just to count them.
    /// </remarks>
    public static readonly Expression<Func<Project, ProjectResponse>> ToResponse =
        project => new ProjectResponse(
            project.Id,
            project.Name,
            project.Description,
            project.Schedule.Start,
            project.Schedule.End,
            project.Status,
            project.Assignments.Count);
}
