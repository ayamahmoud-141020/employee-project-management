using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.ProjectAssignments.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.ProjectAssignments.AssignEmployeeToProject;

public sealed record AssignEmployeeRequest(
    int EmployeeId,
    string Role,
    DateOnly AssignedDate,
    int AllocationPercentage);

internal sealed class AssignEmployeeToProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("projects/{projectId:int}/employees", async (
                int projectId,
                AssignEmployeeRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new AssignEmployeeToProjectCommand(
                    projectId, request.EmployeeId, request.Role, request.AssignedDate, request.AllocationPercentage);

                var result = await sender.Send(command, ct);

                return result.ToCreatedResult(_ => $"/api/projects/{projectId}/employees");
            })
            .WithName("AssignEmployeeToProject")
            .WithTags("Project assignments")
            .WithSummary("Assign an employee to a project")
            .WithDescription(
                """
                Refused with 409 when the employee is inactive or already on the project,
                and with 400 when the allocation is outside 1-100 or the assigned date falls
                outside the project's schedule.
                """)
            .RequireAuthorization(Policies.CanManageAssignments)
            .Produces<ApiResponse<ProjectAssignmentResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
