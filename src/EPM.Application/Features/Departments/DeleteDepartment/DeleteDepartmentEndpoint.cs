using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Departments.DeleteDepartment;

internal sealed class DeleteDepartmentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("departments/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeleteDepartmentCommand(id), ct);

                return result.ToHttpResult("Department deleted.");
            })
            .WithName("DeleteDepartment")
            .WithTags("Departments")
            .WithSummary("Delete a department")
            .WithDescription("Refused with 409 while any employee, active or inactive, still belongs to it.")
            .RequireAuthorization(Policies.CanManageDepartments)
            .Produces<ApiResponse>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
