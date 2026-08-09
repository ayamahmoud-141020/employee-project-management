using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Departments.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Departments.GetDepartments;

internal sealed class GetDepartmentsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("departments", async (string? search, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetDepartmentsQuery(search), ct);

                return result.ToHttpResult();
            })
            .WithName("GetDepartments")
            .WithTags("Departments")
            .WithSummary("List departments")
            .WithDescription("Returns all departments. Not paged — the employee form needs the complete list.")
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<IReadOnlyList<DepartmentResponse>>>();
    }
}
