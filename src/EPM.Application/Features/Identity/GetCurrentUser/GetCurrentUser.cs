using EPM.Application.Abstractions;
using EPM.Application.Common.Http;
using EPM.Domain.Abstractions;
using EPM.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Identity.GetCurrentUser;

public sealed record CurrentUserResponse(
    int Id,
    string Email,
    string DisplayName,
    string Role,
    int? EmployeeId,
    bool IsExternalIdentity);

/// <summary>
/// Who the caller is, according to the server.
/// </summary>
/// <remarks>
/// The Angular app calls this on load rather than decoding the JWT client-side. A token is
/// only base64 — the browser can read its claims but cannot trust them, and an SSO user's
/// role is assigned server-side during claims transformation anyway, so the token the client
/// holds is not the last word on it.
/// </remarks>
public sealed record GetCurrentUserQuery : IRequest<Result<CurrentUserResponse>>;

internal sealed class GetCurrentUserHandler(IAppDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    public async Task<Result<CurrentUserResponse>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure<CurrentUserResponse>(IdentityErrors.InvalidCredentials);
        }

        var user = await context.Users
            .AsNoTracking()
            .Where(entity => entity.Id == userId)
            .Select(entity => new CurrentUserResponse(
                entity.Id,
                entity.Email.Value,
                entity.DisplayName,
                RoleNames.From(entity.Role),
                entity.EmployeeId,
                entity.ExternalObjectId != null))
            .FirstOrDefaultAsync(cancellationToken);

        // A valid token for a user row that has since been deleted. Rare, but it should read
        // as "not signed in" rather than crash.
        return user is null
            ? Result.Failure<CurrentUserResponse>(IdentityErrors.InvalidCredentials)
            : Result.Success(user);
    }
}

internal sealed class GetCurrentUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("auth/me", async (ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetCurrentUserQuery(), ct);

                return result.ToHttpResult();
            })
            .WithName("GetCurrentUser")
            .WithTags("Authentication")
            .WithSummary("Details of the signed-in account")
            .RequireAuthorization()
            .Produces<ApiResponse<CurrentUserResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status401Unauthorized);
    }
}
