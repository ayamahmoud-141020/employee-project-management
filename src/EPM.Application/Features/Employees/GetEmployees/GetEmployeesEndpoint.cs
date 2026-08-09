using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Employees.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Employees.GetEmployees;

internal sealed class GetEmployeesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Parameters are listed individually rather than bound to one object: minimal APIs
        // only bind complex types from the body, and Swagger then documents each one with its
        // own description instead of a single opaque blob.
        app.MapGet("employees", async (
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] string? search,
                [FromQuery] string? sortBy,
                [FromQuery] bool? sortDescending,
                [FromQuery] int? departmentId,
                [FromQuery] bool? isActive,
                [FromQuery] DateOnly? hiredFrom,
                [FromQuery] DateOnly? hiredTo,
                ISender sender,
                CancellationToken ct) =>
            {
                var paging = new PagingOptions
                {
                    Page = page ?? 1,
                    PageSize = pageSize ?? PagingOptions.DefaultPageSize,
                    Search = search,
                    SortBy = sortBy,
                    SortDescending = sortDescending ?? false,
                };

                var result = await sender.Send(
                    new GetEmployeesQuery(paging, departmentId, isActive, hiredFrom, hiredTo), ct);

                return result.ToHttpResult();
            })
            .WithName("GetEmployees")
            .WithTags("Employees")
            .WithSummary("List employees")
            .WithDescription(
                """
                Search matches first name, last name, email or job title.
                Sortable columns: firstName, lastName, email, jobTitle, department, hireDate, isActive.
                Omit `isActive` to include both active and inactive employees.
                """)
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<PagedResult<EmployeeResponse>>>();
    }
}
