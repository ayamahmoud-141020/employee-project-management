using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Projects.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Projects.CreateProject;

internal sealed class CreateProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("projects", async (CreateProjectCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);

                return result.ToCreatedResult(project => $"/api/projects/{project.Id}");
            })
            .WithName("CreateProject")
            .WithTags("Projects")
            .WithSummary("Create a project")
            .WithDescription("Omit endDate for an open-ended project.")
            .RequireAuthorization(Policies.CanManageProjects)
            .Produces<ApiResponse<ProjectResponse>>(StatusCodes.Status201Created)
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
