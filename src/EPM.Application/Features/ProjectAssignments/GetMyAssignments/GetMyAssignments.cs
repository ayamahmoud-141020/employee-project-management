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

namespace EPM.Application.Features.ProjectAssignments.GetMyAssignments;

public sealed record MyAssignmentResponse(
    int ProjectId,
    string ProjectName,
    string? ProjectDescription,
    ProjectStatus ProjectStatus,
    DateOnly ProjectStartDate,
    DateOnly? ProjectEndDate,
    string Role,
    DateOnly AssignedDate,
    int AllocationPercentage);

/// <summary>
/// The signed-in user's own project assignments.
/// </summary>
/// <remarks>
/// Takes no employee id, and that is the security property: the id comes from the token via
/// ICurrentUser, so a User-role account cannot read someone else's assignments by changing a
/// parameter. This is what makes the "User can view their project assignments" rule from the
/// spec actually enforceable rather than advisory.
/// </remarks>
public sealed record GetMyAssignmentsQuery : IRequest<Result<IReadOnlyList<MyAssignmentResponse>>>;

internal sealed class GetMyAssignmentsHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetMyAssignmentsQuery, Result<IReadOnlyList<MyAssignmentResponse>>>
{
    public async Task<Result<IReadOnlyList<MyAssignmentResponse>>> Handle(
        GetMyAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        // An account with no linked employee record — a service admin, say — legitimately has
        // no assignments. Empty list, not an error.
        if (currentUser.EmployeeId is not { } employeeId)
        {
            return Result.Success<IReadOnlyList<MyAssignmentResponse>>([]);
        }

        var assignments = await context.ProjectAssignments
            .AsNoTracking()
            .Where(assignment => assignment.EmployeeId == employeeId)
            .OrderBy(assignment => assignment.Project!.Status)
            .ThenByDescending(assignment => assignment.AssignedDate)
            .Select(assignment => new MyAssignmentResponse(
                assignment.ProjectId,
                assignment.Project!.Name,
                assignment.Project.Description,
                assignment.Project.Status,
                assignment.Project.Schedule.Start,
                assignment.Project.Schedule.End,
                assignment.Role,
                assignment.AssignedDate,
                assignment.Allocation.Percentage))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<MyAssignmentResponse>>(assignments);
    }
}

internal sealed class GetMyAssignmentsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("me/assignments", async (ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetMyAssignmentsQuery(), ct);

                return result.ToHttpResult();
            })
            .WithName("GetMyAssignments")
            .WithTags("Project assignments")
            .WithSummary("List the signed-in user's project assignments")
            .WithDescription("Scoped to the caller's own employee record; there is no parameter to widen it.")
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<IReadOnlyList<MyAssignmentResponse>>>();
    }
}
