using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.ProjectAssignments.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Projects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.ProjectAssignments.GetProjectEmployees;

public sealed record GetProjectEmployeesQuery(int ProjectId)
    : IRequest<Result<IReadOnlyList<ProjectAssignmentResponse>>>;

internal sealed class GetProjectEmployeesHandler(IAppDbContext context)
    : IRequestHandler<GetProjectEmployeesQuery, Result<IReadOnlyList<ProjectAssignmentResponse>>>
{
    public async Task<Result<IReadOnlyList<ProjectAssignmentResponse>>> Handle(
        GetProjectEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        // Checked separately so an unknown project is a 404 rather than an empty list —
        // "this project has no team" and "this project does not exist" are different answers.
        var projectExists = await context.Projects
            .AnyAsync(project => project.Id == query.ProjectId, cancellationToken);

        if (!projectExists)
        {
            return Result.Failure<IReadOnlyList<ProjectAssignmentResponse>>(
                ProjectErrors.NotFound(query.ProjectId));
        }

        var team = await context.ProjectAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ProjectId == query.ProjectId)
            .OrderByDescending(assignment => assignment.Allocation.Percentage)
            .ThenBy(assignment => assignment.Employee!.LastName)
            .Select(assignment => new ProjectAssignmentResponse(
                assignment.Id,
                assignment.EmployeeId,
                assignment.Employee!.FirstName + " " + assignment.Employee.LastName,
                assignment.Employee.Email.Value,
                assignment.Employee.Department != null ? assignment.Employee.Department.Name : string.Empty,
                assignment.Employee.IsActive,
                assignment.Role,
                assignment.AssignedDate,
                assignment.Allocation.Percentage))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProjectAssignmentResponse>>(team);
    }
}

internal sealed class GetProjectEmployeesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("projects/{projectId:int}/employees", async (
                int projectId,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new GetProjectEmployeesQuery(projectId), ct);

                return result.ToHttpResult();
            })
            .WithName("GetProjectEmployees")
            .WithTags("Project assignments")
            .WithSummary("List the employees assigned to a project")
            .WithDescription("Ordered by allocation, highest first. Not paged — a project team is a handful of people.")
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<IReadOnlyList<ProjectAssignmentResponse>>>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);
    }
}
