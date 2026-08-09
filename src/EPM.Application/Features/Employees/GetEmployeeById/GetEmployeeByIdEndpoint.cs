using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Employees.GetEmployeeById;

internal sealed class GetEmployeeByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("employees/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetEmployeeByIdQuery(id), ct);

                return result.ToHttpResult();
            })
            .WithName("GetEmployeeById")
            .WithTags("Employees")
            .WithSummary("Get an employee by id, with their project assignments")
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<EmployeeDetailResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);
    }
}
