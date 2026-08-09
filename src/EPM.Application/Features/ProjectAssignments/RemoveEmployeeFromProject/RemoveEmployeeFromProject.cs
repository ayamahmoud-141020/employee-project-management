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

namespace EPM.Application.Features.ProjectAssignments.RemoveEmployeeFromProject;

public sealed record RemoveEmployeeFromProjectCommand(int ProjectId, int EmployeeId) : IRequest<Result>;

internal sealed class RemoveEmployeeFromProjectHandler(IAppDbContext context)
    : IRequestHandler<RemoveEmployeeFromProjectCommand, Result>
{
    public async Task<Result> Handle(
        RemoveEmployeeFromProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await context.Projects
            .Include(entity => entity.Assignments)
            .FirstOrDefaultAsync(entity => entity.Id == command.ProjectId, cancellationToken);

        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound(command.ProjectId));
        }

        // Removal goes through the aggregate rather than deleting the assignment row
        // directly, so the in-memory collection and the database never disagree about who is
        // on the team.
        var removal = project.RemoveEmployee(command.EmployeeId);

        if (removal.IsFailure)
        {
            return removal;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class RemoveEmployeeFromProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("projects/{projectId:int}/employees/{employeeId:int}", async (
                int projectId,
                int employeeId,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new RemoveEmployeeFromProjectCommand(projectId, employeeId), ct);

                return result.ToHttpResult("Employee removed from the project.");
            })
            .WithName("RemoveEmployeeFromProject")
            .WithTags("Project assignments")
            .WithSummary("Remove an employee from a project")
            .RequireAuthorization(Policies.CanManageAssignments)
            .Produces<ApiResponse>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);
    }
}
