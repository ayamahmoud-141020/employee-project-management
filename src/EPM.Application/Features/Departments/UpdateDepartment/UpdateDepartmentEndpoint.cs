using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Departments.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Departments.UpdateDepartment;

/// <summary>Body of PUT /api/departments/{id}; the id comes from the route, not the payload.</summary>
public sealed record UpdateDepartmentRequest(string Name, string? Description);

internal sealed class UpdateDepartmentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("departments/{id:int}", async (
                int id,
                UpdateDepartmentRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                // Route id wins. Taking it from the body as well would let a caller PUT to
                // /departments/1 with an id of 2 and edit a different row.
                var result = await sender.Send(new UpdateDepartmentCommand(id, request.Name, request.Description), ct);

                return result.ToHttpResult();
            })
            .WithName("UpdateDepartment")
            .WithTags("Departments")
            .WithSummary("Update a department")
            .RequireAuthorization(Policies.CanManageDepartments)
            .Produces<ApiResponse<DepartmentResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
