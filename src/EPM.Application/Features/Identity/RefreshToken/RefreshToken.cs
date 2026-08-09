using EPM.Application.Abstractions;
using EPM.Application.Common.Http;
using EPM.Application.Features.Identity.Login;
using EPM.Domain.Abstractions;
using EPM.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Identity.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthenticationResponse>>;

internal sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() =>
        RuleFor(command => command.RefreshToken).NotEmpty().WithMessage("A refresh token is required.");
}

/// <summary>
/// Trades a refresh token for a new access token, rotating the refresh token as it goes.
/// </summary>
internal sealed class RefreshTokenHandler(
    IAppDbContext context,
    ITokenService tokenService,
    IDateTimeProvider clock)
    : IRequestHandler<RefreshTokenCommand, Result<AuthenticationResponse>>
{
    public async Task<Result<AuthenticationResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        // Found through the token's own row and then loaded with the whole collection, because
        // rotation revokes the presented one and adds a replacement — both are changes to the
        // AppUser aggregate.
        var userId = await context.RefreshTokens
            .Where(token => token.Token == command.RefreshToken)
            .Select(token => token.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (userId == 0)
        {
            return Result.Failure<AuthenticationResponse>(IdentityErrors.RefreshTokenInvalid);
        }

        var user = await context.Users
            .Include(entity => entity.RefreshTokens)
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthenticationResponse>(IdentityErrors.RefreshTokenInvalid);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthenticationResponse>(IdentityErrors.AccountDisabled);
        }

        var rotated = user.RotateRefreshToken(
            command.RefreshToken,
            tokenService.CreateRefreshToken(),
            tokenService.GetRefreshTokenExpiry(clock.UtcNow),
            clock.UtcNow);

        if (rotated.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(rotated.Error);
        }

        var accessToken = tokenService.CreateAccessToken(user);

        await context.SaveChangesAsync(cancellationToken);

        return new AuthenticationResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            rotated.Value.Token,
            new AuthenticatedUser(
                user.Id,
                user.Email.Value,
                user.DisplayName,
                RoleNames.From(user.Role),
                user.EmployeeId));
    }
}

internal sealed class RefreshTokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("auth/refresh", async (RefreshTokenCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);

                return result.ToHttpResult();
            })
            .WithName("RefreshToken")
            .WithTags("Authentication")
            .WithSummary("Exchange a refresh token for a new access token")
            .WithDescription("The presented refresh token is revoked and replaced, so each one works exactly once.")
            // Anonymous by design: the whole point is to call this once the access token has
            // expired, when there is nothing left to authenticate with.
            .AllowAnonymous()
            .Produces<ApiResponse<AuthenticationResponse>>()
            .Produces<ApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiResponse>(StatusCodes.Status401Unauthorized);
    }
}
