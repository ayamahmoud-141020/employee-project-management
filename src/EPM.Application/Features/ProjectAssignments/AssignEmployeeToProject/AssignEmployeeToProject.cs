using EPM.Application.Abstractions;
using EPM.Application.Features.ProjectAssignments.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Employees;
using EPM.Domain.Projects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.ProjectAssignments.AssignEmployeeToProject;

public sealed record AssignEmployeeToProjectCommand(
    int ProjectId,
    int EmployeeId,
    string Role,
    DateOnly AssignedDate,
    int AllocationPercentage) : IRequest<Result<ProjectAssignmentResponse>>;

internal sealed class AssignEmployeeToProjectValidator : AbstractValidator<AssignEmployeeToProjectCommand>
{
    public AssignEmployeeToProjectValidator()
    {
        RuleFor(command => command.ProjectId).GreaterThan(0);
        RuleFor(command => command.EmployeeId).GreaterThan(0).WithMessage("An employee must be selected.");

        RuleFor(command => command.Role)
            .NotEmpty().WithMessage("A role is required for the assignment.")
            .MaximumLength(ProjectAssignment.MaxRoleLength);

        RuleFor(command => command.AllocationPercentage)
            .InclusiveBetween(Allocation.Minimum, Allocation.Maximum)
            .WithMessage($"Allocation percentage must be between {Allocation.Minimum} and {Allocation.Maximum}.");
    }
}

/// <summary>
/// Adds an employee to a project team.
/// </summary>
/// <remarks>
/// The handler's only real job is to gather what the aggregate cannot see for itself. The
/// four business rules — active employee, no duplicate, allocation range, date inside the
/// schedule — are all enforced by Project.AssignEmployee, not here. That is deliberate: a
/// second caller (an import, a bulk-assign feature) gets the same guarantees for free.
/// </remarks>
internal sealed class AssignEmployeeToProjectHandler(IAppDbContext context)
    : IRequestHandler<AssignEmployeeToProjectCommand, Result<ProjectAssignmentResponse>>
{
    public async Task<Result<ProjectAssignmentResponse>> Handle(
        AssignEmployeeToProjectCommand command,
        CancellationToken cancellationToken)
    {
        // The existing assignments have to be loaded, or the aggregate cannot tell whether
        // this is a duplicate.
        var project = await context.Projects
            .Include(entity => entity.Assignments)
            .FirstOrDefaultAsync(entity => entity.Id == command.ProjectId, cancellationToken);

        if (project is null)
        {
            return Result.Failure<ProjectAssignmentResponse>(ProjectErrors.NotFound(command.ProjectId));
        }

        var employee = await context.Employees
            .AsNoTracking()
            .Include(entity => entity.Department)
            .FirstOrDefaultAsync(entity => entity.Id == command.EmployeeId, cancellationToken);

        if (employee is null)
        {
            return Result.Failure<ProjectAssignmentResponse>(EmployeeErrors.NotFound(command.EmployeeId));
        }

        // IsActive is passed as a value rather than the whole employee: Project must not hold
        // a reference into another aggregate, it just needs the answer to one question.
        var assignment = project.AssignEmployee(
            employee.Id,
            employee.IsActive,
            command.Role,
            command.AssignedDate,
            command.AllocationPercentage);

        if (assignment.IsFailure)
        {
            return Result.Failure<ProjectAssignmentResponse>(assignment.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        var created = project.Assignments.Single(entity => entity.EmployeeId == employee.Id);

        return new ProjectAssignmentResponse(
            created.Id,
            employee.Id,
            employee.FullName,
            employee.Email.Value,
            employee.Department?.Name ?? string.Empty,
            employee.IsActive,
            created.Role,
            created.AssignedDate,
            created.Allocation.Percentage);
    }
}
