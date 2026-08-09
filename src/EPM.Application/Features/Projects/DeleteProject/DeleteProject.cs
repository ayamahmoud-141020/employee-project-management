using EPM.Application.Abstractions;
using EPM.Application.Common;
using EPM.Application.Common.Http;
using EPM.Domain.Abstractions;
using EPM.Domain.Projects;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Projects.DeleteProject;

/// <summary>
/// Hard delete, unlike employees.
/// </summary>
/// <remarks>
/// The spec asks for soft deletion on employees specifically, and the reason does not carry
/// over: nothing references a project the way assignments reference an employee. Its
/// assignments cascade away with it, which is correct — an assignment to a project that no
/// longer exists is meaningless. Cancelling a project that should stay on the books is what
/// the Cancelled status is for.
/// </remarks>
public sealed record DeleteProjectCommand(int Id) : IRequest<Result>;

internal sealed class DeleteProjectHandler(IAppDbContext context)
    : IRequestHandler<DeleteProjectCommand, Result>
{
    public async Task<Result> Handle(DeleteProjectCommand command, CancellationToken cancellationToken)
    {
        var project = await context.Projects
            .FirstOrDefaultAsync(entity => entity.Id == command.Id, cancellationToken);

        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound(command.Id));
        }

        context.Projects.Remove(project);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

internal sealed class DeleteProjectEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("projects/{id:int}", async (int id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DeleteProjectCommand(id), ct);

                return result.ToHttpResult("Project deleted.");
            })
            .WithName("DeleteProject")
            .WithTags("Projects")
            .WithSummary("Delete a project")
            .WithDescription("Permanent. Its assignments are removed with it; consider the Cancelled status instead.")
            .RequireAuthorization(Policies.CanManageProjects)
            .Produces<ApiResponse>()
            .Produces<ApiResponse>(StatusCodes.Status404NotFound);
    }
}
