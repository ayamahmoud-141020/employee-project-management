using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Departments.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Departments.GetDepartmentById;

internal sealed class GetDepartmentByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("departments/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetDepartmentByIdQuery(id), ct);

                return result.ToHttpResult();
            })
            .WithName("GetDepartmentById")
            .WithTags("Departments")
            .WithSummary("Get a department by id")
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<DepartmentResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);
    }
}
