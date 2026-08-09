using EPM.Application.Abstractions;
using EPM.Application.Features.Departments.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Departments;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Departments.UpdateDepartment;

public sealed record UpdateDepartmentCommand(int Id, string Name, string? Description)
    : IRequest<Result<DepartmentResponse>>;

internal sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(Department.MaxNameLength);

        RuleFor(command => command.Description)
            .MaximumLength(Department.MaxDescriptionLength);
    }
}

internal sealed class UpdateDepartmentHandler(IAppDbContext context)
    : IRequestHandler<UpdateDepartmentCommand, Result<DepartmentResponse>>
{
    public async Task<Result<DepartmentResponse>> Handle(
        UpdateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        var department = await context.Departments
            .FirstOrDefaultAsync(entity => entity.Id == command.Id, cancellationToken);

        if (department is null)
        {
            return Result.Failure<DepartmentResponse>(DepartmentErrors.NotFound(command.Id));
        }

        var name = command.Name.Trim();

        // Excludes itself: renaming a department to the name it already has is a no-op, not
        // a collision.
        var nameTaken = await context.Departments
            .AnyAsync(other => other.Id != command.Id && other.Name == name, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<DepartmentResponse>(DepartmentErrors.NameAlreadyExists);
        }

        var update = department.Update(name, command.Description);

        if (update.IsFailure)
        {
            return Result.Failure<DepartmentResponse>(update.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        var employeeCount = await context.Employees
            .CountAsync(employee => employee.DepartmentId == department.Id, cancellationToken);

        var activeEmployeeCount = await context.Employees
            .CountAsync(employee => employee.DepartmentId == department.Id && employee.IsActive, cancellationToken);

        return new DepartmentResponse(
            department.Id,
            department.Name,
            department.Description,
            employeeCount,
            activeEmployeeCount);
    }
}
