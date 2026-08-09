using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Domain.Abstractions;
using EPM.Domain.Employees;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Employees.ReactivateEmployee;

/// <summary>
/// Restores a deactivated employee.
/// </summary>
/// <remarks>
/// Not in the spec's endpoint list, but soft deletion without a way back is a trap: the only
/// alternative for an accidental deactivation would be editing the database by hand. Their
/// previous project assignments are not restored — those were removed on deactivation, and
/// re-adding them silently would be a worse surprise than re-assigning deliberately.
/// </remarks>
public sealed record ReactivateEmployeeCommand(int Id) : IRequest<Result>;

internal sealed class ReactivateEmployeeHandler(IAppDbContext context)
    : IRequestHandler<ReactivateEmployeeCommand, Result>
{
    public async Task<Result> Handle(ReactivateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .FirstOrDefaultAsync(entity => entity.Id == command.Id, cancellationToken);

        if (employee is null)
        {
            return Result.Failure(EmployeeErrors.NotFound(command.Id));
        }

        var reactivation = employee.Reactivate();

        if (reactivation.IsFailure)
        {
            return reactivation;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class ReactivateEmployeeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("employees/{id:int}/reactivate", async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ReactivateEmployeeCommand(id), ct);

                return result.ToHttpResult("Employee reactivated.");
            })
            .WithName("ReactivateEmployee")
            .WithTags("Employees")
            .WithSummary("Reactivate a deactivated employee")
            .WithDescription("Previous project assignments are not restored — reassign them explicitly.")
            .RequireAuthorization(Policies.CanManageEmployees)
            .Produces<ApiResponse>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
