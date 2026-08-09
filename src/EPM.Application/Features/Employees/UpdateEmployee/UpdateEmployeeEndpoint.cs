using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Employees.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Employees.UpdateEmployee;

public sealed record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string JobTitle,
    int DepartmentId,
    DateOnly HireDate);

internal sealed class UpdateEmployeeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("employees/{id:int}", async (
                int id,
                UpdateEmployeeRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new UpdateEmployeeCommand(
                    id,
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.Phone,
                    request.JobTitle,
                    request.DepartmentId,
                    request.HireDate);

                var result = await sender.Send(command, ct);

                return result.ToHttpResult();
            })
            .WithName("UpdateEmployee")
            .WithTags("Employees")
            .WithSummary("Update an employee")
            .WithDescription("Does not change active status — use DELETE to deactivate or POST .../reactivate to restore.")
            .RequireAuthorization(Policies.CanManageEmployees)
            .Produces<ApiResponse<EmployeeResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
