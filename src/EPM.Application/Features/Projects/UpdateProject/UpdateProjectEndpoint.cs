using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Projects.Contracts;
using EPM.Domain.Projects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Projects.UpdateProject;

public sealed record UpdateProjectRequest(
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly? EndDate,
    ProjectStatus Status);

internal sealed class UpdateProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("projects/{id:int}", async (
                int id,
                UpdateProjectRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new UpdateProjectCommand(
                    id, request.Name, request.Description, request.StartDate, request.EndDate, request.Status);

                var result = await sender.Send(command, ct);

                return result.ToHttpResult();
            })
            .WithName("UpdateProject")
            .WithTags("Projects")
            .WithSummary("Update a project")
            .WithDescription(
                "Refused with 409 if the new schedule would leave an existing assignment outside the project dates.")
            .RequireAuthorization(Policies.CanManageProjects)
            .Produces<ApiResponse<ProjectResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiResponse>(StatusCodes.Status409Conflict);
    }
}
