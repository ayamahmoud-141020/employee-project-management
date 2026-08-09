using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Employees.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Employees.CreateEmployee;

internal sealed class CreateEmployeeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("employees", async (CreateEmployeeCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);

                return result.ToCreatedResult(employee => $"/api/employees/{employee.Id}");
            })
            .WithName("CreateEmployee")
            .WithTags("Employees")
            .WithSummary("Create an employee")
            .RequireAuthorization(Policies.CanManageEmployees)
            .Produces<ApiResponse<EmployeeResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
