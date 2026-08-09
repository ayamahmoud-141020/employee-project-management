using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Application.Features.Projects.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Projects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Projects.GetProjectById;

public sealed record GetProjectByIdQuery(int Id) : IRequest<Result<ProjectResponse>>;

internal sealed class GetProjectByIdHandler(IAppDbContext context)
    : IRequestHandler<GetProjectByIdQuery, Result<ProjectResponse>>
{
    public async Task<Result<ProjectResponse>> Handle(
        GetProjectByIdQuery query,
        CancellationToken cancellationToken)
    {
        var project = await context.Projects
            .AsNoTracking()
            .Where(entity => entity.Id == query.Id)
            .Select(ProjectProjections.ToResponse)
            .FirstOrDefaultAsync(cancellationToken);

        return project is null
            ? Result.Failure<ProjectResponse>(ProjectErrors.NotFound(query.Id))
            : Result.Success(project);
    }
}

internal sealed class GetProjectByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("projects/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetProjectByIdQuery(id), ct);

                return result.ToHttpResult();
            })
            .WithName("GetProjectById")
            .WithTags("Projects")
            .WithSummary("Get a project by id")
            .WithDescription("The team is a separate call: GET /api/projects/{id}/employees.")
            .RequireAuthorization(Policies.CanViewDirectory)
            .Produces<ApiResponse<ProjectResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);
    }
}
