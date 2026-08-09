using EPM.Application.Abstractions;
using EPM.Application.Features.Employees.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Departments;
using EPM.Domain.Employees;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Employees.CreateEmployee;

public sealed record CreateEmployeeCommand(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string JobTitle,
    int DepartmentId,
    DateOnly HireDate) : IRequest<Result<EmployeeResponse>>;

internal sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    /// <remarks>
    /// These rules overlap with Employee.Create on purpose, and the duplication is worth it.
    /// The validator runs first and reports every broken field at once, keyed by name, so the
    /// form can highlight all of them in one pass. The aggregate reports one error at a time
    /// but is the rule that cannot be bypassed — the seeder, a domain event handler and any
    /// future caller go through it without touching this validator.
    /// </remarks>
    public CreateEmployeeValidator(IDateTimeProvider clock)
    {
        RuleFor(command => command.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(Employee.MaxNameLength);

        RuleFor(command => command.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(Employee.MaxNameLength);

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(Email.MaxLength)
            // Delegates to the value object rather than repeating its regex, so the API and
            // the domain can never disagree about what a valid address looks like.
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

internal sealed class CreateEmployeeHandler(IAppDbContext context, IDateTimeProvider clock)
    : IRequestHandler<CreateEmployeeCommand, Result<EmployeeResponse>>
{
    public async Task<Result<EmployeeResponse>> Handle(
        CreateEmployeeCommand command,
        CancellationToken cancellationToken)
    {
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

        // Compared on .Value, not on the whole object: Email is an owned type, and EF cannot
        // translate equality between two owned instances. Normalising happens in Email.Create,
        // so this comparison is already case-insensitive by construction.
        var normalisedEmail = email.Value.Value;

        var emailTaken = await context.Employees
            .AnyAsync(employee => employee.Email.Value == normalisedEmail, cancellationToken);

        if (emailTaken)
        {
            return Result.Failure<EmployeeResponse>(EmployeeErrors.EmailAlreadyExists);
        }

        var employee = Employee.Create(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Phone,
            command.JobTitle,
            command.DepartmentId,
            command.HireDate,
            clock.Today);

        if (employee.IsFailure)
        {
            return Result.Failure<EmployeeResponse>(employee.Error);
        }

        context.Employees.Add(employee.Value);
        await context.SaveChangesAsync(cancellationToken);

        var departmentName = await context.Departments
            .Where(department => department.Id == command.DepartmentId)
            .Select(department => department.Name)
            .FirstAsync(cancellationToken);

        return new EmployeeResponse(
            employee.Value.Id,
            employee.Value.FirstName,
            employee.Value.LastName,
            employee.Value.FullName,
            employee.Value.Email.Value,
            employee.Value.Phone?.Value,
            employee.Value.JobTitle,
            employee.Value.DepartmentId,
            departmentName,
            employee.Value.HireDate,
            employee.Value.IsActive);
    }
}
