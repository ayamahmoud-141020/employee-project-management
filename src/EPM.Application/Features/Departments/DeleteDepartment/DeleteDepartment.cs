using EPM.Application.Abstractions;
using EPM.Domain.Abstractions;
using EPM.Domain.Departments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Departments.DeleteDepartment;

public sealed record DeleteDepartmentCommand(int Id) : IRequest<Result>;

internal sealed class DeleteDepartmentHandler(IAppDbContext context)
    : IRequestHandler<DeleteDepartmentCommand, Result>
{
    public async Task<Result> Handle(DeleteDepartmentCommand command, CancellationToken cancellationToken)
    {
        var department = await context.Departments
            .FirstOrDefaultAsync(entity => entity.Id == command.Id, cancellationToken);

        if (department is null)
        {
            return Result.Failure(DepartmentErrors.NotFound(command.Id));
        }

        // "A department containing active employees cannot be deleted." The rule spans two
        // aggregates, so it cannot live on Department itself — it is checked here, against
        // the employees table, and backed by the Restrict foreign key which stops a delete
        // getting through even if this check were ever bypassed.
        var activeEmployees = await context.Employees
            .CountAsync(employee => employee.DepartmentId == command.Id && employee.IsActive, cancellationToken);

        if (activeEmployees > 0)
        {
            return Result.Failure(DepartmentErrors.HasActiveEmployees(activeEmployees));
        }

        // Inactive employees still hold a foreign key to this row, so deleting the department
        // out from under them would fail at the database with an opaque message. Refusing
        // here, with a reason, beats a 500.
        var inactiveEmployees = await context.Employees
            .CountAsync(employee => employee.DepartmentId == command.Id, cancellationToken);

        if (inactiveEmployees > 0)
        {
            return Result.Failure(Error.Conflict(
                "Department.HasEmployees",
                $"This department still has {inactiveEmployees} inactive employee(s). " +
                "Move them to another department before deleting it."));
        }

        context.Departments.Remove(department);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
