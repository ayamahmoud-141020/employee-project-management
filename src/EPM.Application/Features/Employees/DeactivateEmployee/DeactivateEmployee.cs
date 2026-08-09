using EPM.Application.Abstractions;
using EPM.Domain.Abstractions;
using EPM.Domain.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Employees.DeactivateEmployee;

/// <summary>
/// Backs DELETE /api/employees/{id}.
/// </summary>
/// <remarks>
/// The route is DELETE because that is what the spec's REST table asks for, but the effect is
/// deactivation — the spec also says soft deletion is preferred, and an employee row is
/// referenced by historical project assignments that would be lost with it. Named for what it
/// does rather than for the verb that triggers it.
/// </remarks>
public sealed record DeactivateEmployeeCommand(int Id) : IRequest<Result>;

internal sealed class DeactivateEmployeeHandler(IAppDbContext context, IDateTimeProvider clock)
    : IRequestHandler<DeactivateEmployeeCommand, Result>
{
    public async Task<Result> Handle(DeactivateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .FirstOrDefaultAsync(entity => entity.Id == command.Id, cancellationToken);

        if (employee is null)
        {
            return Result.Failure(EmployeeErrors.NotFound(command.Id));
        }

        var deactivation = employee.Deactivate(clock.UtcNow);

        if (deactivation.IsFailure)
        {
            return deactivation;
        }

        // Raises EmployeeDeactivated, which the interceptor publishes after this save
        // succeeds. UnassignEmployeeFromProjectsHandler picks it up and clears the open
        // allocations.
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
