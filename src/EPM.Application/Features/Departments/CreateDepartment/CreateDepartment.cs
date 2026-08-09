using EPM.Application.Abstractions;
using EPM.Application.Features.Departments.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Departments;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Departments.CreateDepartment;

public sealed record CreateDepartmentCommand(string Name, string? Description)
    : IRequest<Result<DepartmentResponse>>;

internal sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    // Shape checks only. Whether the name is *taken* is a business rule and lives in the
    // handler, where it can be reported as a 409 rather than a field-level validation error.
    public CreateDepartmentValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Department name is required.")
            .MaximumLength(Department.MaxNameLength);

        RuleFor(command => command.Description)
            .MaximumLength(Department.MaxDescriptionLength);
    }
}

internal sealed class CreateDepartmentHandler(IAppDbContext context)
    : IRequestHandler<CreateDepartmentCommand, Result<DepartmentResponse>>
{
    public async Task<Result<DepartmentResponse>> Handle(
        CreateDepartmentCommand command,
        CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();

        // Checked here so the user gets "that name is taken" instead of a constraint
        // violation. This is not the guarantee — UX_Departments_Name is. Two simultaneous
        // requests can both pass this check, and the second one then fails at the database,
        // which is the correct outcome.
        var nameTaken = await context.Departments
            .AnyAsync(department => department.Name == name, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<DepartmentResponse>(DepartmentErrors.NameAlreadyExists);
        }

        var department = Department.Create(name, command.Description);

        if (department.IsFailure)
        {
            return Result.Failure<DepartmentResponse>(department.Error);
        }

        context.Departments.Add(department.Value);
        await context.SaveChangesAsync(cancellationToken);

        // Counts are zero by definition on a department that was created a line ago, so this
        // is built directly rather than re-queried.
        return new DepartmentResponse(
            department.Value.Id,
            department.Value.Name,
            department.Value.Description,
            EmployeeCount: 0,
            ActiveEmployeeCount: 0);
    }
}
