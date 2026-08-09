using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Projects.Contracts;
using EPM.Domain.Projects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Projects.GetProjects;

internal sealed class GetProjectsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("projects", async (
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] string? search,
                [FromQuery] string? sortBy,
                [FromQuery] bool? sortDescending,
                [FromQuery] ProjectStatus? status,
                [FromQuery] DateOnly? startsFrom,
                [FromQuery] DateOnly? startsTo,
                [FromQuery] int? employeeId,
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
                    new GetProjectsQuery(paging, status, startsFrom, startsTo, employeeId), ct);

                return result.ToHttpResult();
            })
            .WithName("GetProjects")
            .WithTags("Projects")
            .WithSummary("List projects")
            .WithDescription(
                """
                Search matches name and description.
                Sortable columns: name, startDate, endDate, status, assignedEmployeeCount.
                Pass `employeeId` to return only the projects that employee is assigned to.
                """)
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<PagedResult<ProjectResponse>>>();
    }
}
