using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.ProjectAssignments.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Projects;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.ProjectAssignments.UpdateProjectAssignment;

/// <summary>
/// Changes an existing assignment's role or allocation.
/// </summary>
/// <remarks>
/// Not in the spec's endpoint list, but without it the only way to move someone from 50% to
/// 80% is to remove and re-add them, which loses the original assigned date. The assigned
/// date itself is not editable here — changing when someone joined a project is a correction,
/// not an everyday edit, and it would need re-validating against the schedule.
/// </remarks>
public sealed record UpdateProjectAssignmentCommand(
    int ProjectId,
    int EmployeeId,
    string Role,
    int AllocationPercentage) : IRequest<Result<ProjectAssignmentResponse>>;

internal sealed class UpdateProjectAssignmentValidator : AbstractValidator<UpdateProjectAssignmentCommand>
{
    public UpdateProjectAssignmentValidator()
    {
        RuleFor(command => command.Role)
            .NotEmpty().WithMessage("A role is required for the assignment.")
            .MaximumLength(ProjectAssignment.MaxRoleLength);

        RuleFor(command => command.AllocationPercentage)
            .InclusiveBetween(Allocation.Minimum, Allocation.Maximum)
            .WithMessage($"Allocation percentage must be between {Allocation.Minimum} and {Allocation.Maximum}.");
    }
}

internal sealed class UpdateProjectAssignmentHandler(IAppDbContext context)
    : IRequestHandler<UpdateProjectAssignmentCommand, Result<ProjectAssignmentResponse>>
{
    public async Task<Result<ProjectAssignmentResponse>> Handle(
        UpdateProjectAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var project = await context.Projects
            .Include(entity => entity.Assignments)
            .FirstOrDefaultAsync(entity => entity.Id == command.ProjectId, cancellationToken);

        if (project is null)
        {
            return Result.Failure<ProjectAssignmentResponse>(ProjectErrors.NotFound(command.ProjectId));
        }

        var update = project.UpdateAssignment(command.EmployeeId, command.Role, command.AllocationPercentage);

        if (update.IsFailure)
        {
            return Result.Failure<ProjectAssignmentResponse>(update.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        var assignment = project.Assignments.Single(entity => entity.EmployeeId == command.EmployeeId);

        var employee = await context.Employees
            .AsNoTracking()
            .Include(entity => entity.Department)
            .FirstAsync(entity => entity.Id == command.EmployeeId, cancellationToken);

        return new ProjectAssignmentResponse(
            assignment.Id,
            employee.Id,
            employee.FullName,
            employee.Email.Value,
            employee.Department?.Name ?? string.Empty,
            employee.IsActive,
            assignment.Role,
            assignment.AssignedDate,
            assignment.Allocation.Percentage);
    }
}

internal sealed class UpdateProjectAssignmentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("projects/{projectId:int}/employees/{employeeId:int}", async (
                int projectId,
                int employeeId,
                UpdateProjectAssignmentRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new UpdateProjectAssignmentCommand(
                    projectId, employeeId, request.Role, request.AllocationPercentage);

                var result = await sender.Send(command, ct);

                return result.ToHttpResult();
            })
            .WithName("UpdateProjectAssignment")
            .WithTags("Project assignments")
            .WithSummary("Change an assignment's role or allocation")
            .RequireAuthorization(Policies.CanManageAssignments)
            .Produces<ApiResponse<ProjectAssignmentResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateProjectAssignmentRequest(string Role, int AllocationPercentage);
