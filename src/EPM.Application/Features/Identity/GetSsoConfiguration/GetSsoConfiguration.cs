using EPM.Application.Abstractions;
using EPM.Domain.Abstractions;
using MediatR;

namespace EPM.Application.Features.Identity.GetSsoConfiguration;

public sealed record SsoConfigurationResponse(
    bool Enabled,
    string? Authority,
    string? ClientId,
    string? ApiScope);

/// <summary>
/// Whether single sign-on is available, and what the browser needs to use it.
/// </summary>
/// <remarks>
/// The sign-in page asks for this before it decides whether to draw the SSO button, so a
/// deployment turns SSO on by setting <c>EntraId:*</c> on the API alone — the client is built
/// once and configured at runtime, and a tenant id never reaches the bundle.
/// </remarks>
public sealed record GetSsoConfigurationQuery : IRequest<Result<SsoConfigurationResponse>>;

internal sealed class GetSsoConfigurationHandler(ISsoConfigurationProvider provider)
    : IRequestHandler<GetSsoConfigurationQuery, Result<SsoConfigurationResponse>>
{
    public Task<Result<SsoConfigurationResponse>> Handle(
        GetSsoConfigurationQuery query,
        CancellationToken cancellationToken)
    {
        var configuration = provider.Current;

        var response = new SsoConfigurationResponse(
            configuration.Enabled,
            configuration.Authority,
            configuration.ClientId,
            configuration.ApiScope);

        return Task.FromResult(Result.Success(response));
    }
}
