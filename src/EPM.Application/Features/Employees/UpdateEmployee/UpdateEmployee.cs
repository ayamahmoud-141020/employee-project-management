using EPM.Application.Abstractions;
using EPM.Application.Features.Employees.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Departments;
using EPM.Domain.Employees;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Employees.UpdateEmployee;

public sealed record UpdateEmployeeCommand(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string JobTitle,
    int DepartmentId,
    DateOnly HireDate) : IRequest<Result<EmployeeResponse>>;

internal sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeValidator(IDateTimeProvider clock)
    {
        RuleFor(command => command.Id).GreaterThan(0);

        RuleFor(command => command.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(Employee.MaxNameLength);

        RuleFor(command => command.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(Employee.MaxNameLength);

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(Email.MaxLength)
            .Must(email => Domain.Employees.Email.Create(email).IsSuccess)
            .WithMessage("Email must be a valid email address.");

        RuleFor(command => command.JobTitle)
            .NotEmpty().WithMessage("Job title is required.")
            .MaximumLength(Employee.MaxJobTitleLength);

        RuleFor(command => command.DepartmentId)
            .GreaterThan(0).WithMessage("Department is required.");

        RuleFor(command => command.HireDate)
            .LessThanOrEqualTo(_ => clock.Today).WithMessage("Hire date cannot be in the future.");
    }
}

internal sealed class UpdateEmployeeHandler(IAppDbContext context, IDateTimeProvider clock)
    : IRequestHandler<UpdateEmployeeCommand, Result<EmployeeResponse>>
{
    public async Task<Result<EmployeeResponse>> Handle(
        UpdateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .FirstOrDefaultAsync(entity => entity.Id == command.Id, cancellationToken);

        if (employee is null)
        {
            return Result.Failure<EmployeeResponse>(EmployeeErrors.NotFound(command.Id));
        }

        var departmentExists = await context.Departments
            .AnyAsync(department => department.Id == command.DepartmentId, cancellationToken);

        if (!departmentExists)
        {
            return Result.Failure<EmployeeResponse>(DepartmentErrors.NotFound(command.DepartmentId));
        }

        var email = Domain.Employees.Email.Create(command.Email);

        if (email.IsFailure)
        {
            return Result.Failure<EmployeeResponse>(email.Error);
        }

        var normalisedEmail = email.Value.Value;

        // Excludes this employee — keeping your own address on an edit is not a duplicate.
        var emailTaken = await context.Employees
            .AnyAsync(other => other.Id != command.Id && other.Email.Value == normalisedEmail, cancellationToken);

        if (emailTaken)
        {
            return Result.Failure<EmployeeResponse>(EmployeeErrors.EmailAlreadyExists);
        }

        var update = employee.Update(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Phone,
            command.JobTitle,
            command.DepartmentId,
            command.HireDate,
            clock.Today);

        if (update.IsFailure)
        {
            return Result.Failure<EmployeeResponse>(update.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        var departmentName = await context.Departments
            .Where(department => department.Id == command.DepartmentId)
            .Select(department => department.Name)
            .FirstAsync(cancellationToken);

        return new EmployeeResponse(
            employee.Id,
            employee.FirstName,
            employee.LastName,
            employee.FullName,
            employee.Email.Value,
            employee.Phone?.Value,
            employee.JobTitle,
            employee.DepartmentId,
            departmentName,
            employee.HireDate,
            employee.IsActive);
    }
}
