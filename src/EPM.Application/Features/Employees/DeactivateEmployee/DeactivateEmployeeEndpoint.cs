using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Employees.DeactivateEmployee;

internal sealed class DeactivateEmployeeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("employees/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeactivateEmployeeCommand(id), ct);

                return result.ToHttpResult("Employee deactivated.");
            })
            .WithName("DeactivateEmployee")
            .WithTags("Employees")
            .WithSummary("Deactivate an employee")
            .WithDescription(
                "Soft delete. The record is kept and marked inactive so project history survives, " +
                "and the employee is removed from any project they were still assigned to.")
            .RequireAuthorization(Policies.CanManageEmployees)
            .Produces<ApiResponse>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
