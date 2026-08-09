using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Departments.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Departments.CreateDepartment;

internal sealed class CreateDepartmentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("departments", async (CreateDepartmentCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);

                return result.ToCreatedResult(department => $"/api/departments/{department.Id}");
            })
            .WithName("CreateDepartment")
            .WithTags("Departments")
            .WithSummary("Create a department")
            .RequireAuthorization(Policies.CanManageDepartments)
            .Produces<ApiResponse<DepartmentResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            // 409 rather than 400 when the name is taken — see ResultExtensions.
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
