using EPM.Application.Abstractions;
using EPM.Application.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Identity.Login;

internal sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("auth/login", async (LoginCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);

                return result.ToHttpResult();
            })
            .WithName("Login")
            .WithTags("Authentication")
            .WithSummary("Exchange email and password for an access token")
            .WithDescription("Seeded accounts: admin@epm.local, manager@epm.local, user@epm.local.")
            // The only write endpoint that is open — everything else needs the token this
            // returns.
            .AllowAnonymous()
            .Produces<ApiResponse<AuthenticationResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse>(StatusCodes.Status403Forbidden);
    }
}
