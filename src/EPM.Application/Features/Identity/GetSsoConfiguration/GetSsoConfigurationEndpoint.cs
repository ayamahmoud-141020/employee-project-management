using EPM.Application.Abstractions;
using EPM.Application.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Features.Identity.GetSsoConfiguration;

internal sealed class GetSsoConfigurationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("auth/sso", async (ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetSsoConfigurationQuery(), ct);

                return result.ToHttpResult();
            })
            .WithName("GetSsoConfiguration")
            .WithTags("Authentication")
            .WithSummary("Whether single sign-on is available, and the client settings for it")
            .WithDescription(
                "Returns public app-registration values only. Anonymous by design: the sign-in " +
                "page has to read it before anyone is signed in.")
            .AllowAnonymous()
            .Produces<ApiResponse<SsoConfigurationResponse>>();
    }
}
