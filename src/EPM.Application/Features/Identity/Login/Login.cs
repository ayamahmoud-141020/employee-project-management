using EPM.Application.Abstractions;
using EPM.Domain.Abstractions;
using EPM.Domain.Employees;
using EPM.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Identity.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthenticationResponse>>;

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    AuthenticatedUser User);

public sealed record AuthenticatedUser(
    int Id,
    string Email,
    string DisplayName,
    string Role,
    int? EmployeeId);

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email).NotEmpty().WithMessage("Email is required.");
        RuleFor(command => command.Password).NotEmpty().WithMessage("Password is required.");
    }
}

internal sealed class LoginHandler(
    IAppDbContext context,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IDateTimeProvider clock)
    : IRequestHandler<LoginCommand, Result<AuthenticationResponse>>
{
    public async Task<Result<AuthenticationResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(command.Email);

        if (email.IsFailure)
        {
            // A malformed address gets the same answer as a wrong password. Saying "that is
            // not a valid email" here would be harmless, but keeping one response for every
            // failed login means no branch of this method leaks whether an account exists.
            return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidCredentials);
        }

        var normalisedEmail = email.Value.Value;

        var user = await context.Users
            .FirstOrDefaultAsync(entity => entity.Email.Value == normalisedEmail, cancellationToken);

        if (user is null)
        {
            // Burn the same work a real verification would cost — see IPasswordHasher.DummyHash.
            passwordHasher.Verify(command.Password, passwordHasher.DummyHash);

            return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidCredentials);
        }

        var canSignIn = user.EnsureCanSignInWithPassword();

        if (canSignIn.IsFailure)
        {
            return Result.Failure<AuthenticationResponse>(canSignIn.Error);
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash!))
        {
            return Result.Failure<AuthenticationResponse>(IdentityErrors.InvalidCredentials);
        }

        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = user.IssueRefreshToken(
            tokenService.CreateRefreshToken(),
            tokenService.GetRefreshTokenExpiry(clock.UtcNow),
            clock.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        return new AuthenticationResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.Token,
            new AuthenticatedUser(
                user.Id,
                user.Email.Value,
                user.DisplayName,
                RoleNames.From(user.Role),
                user.EmployeeId));
    }
}
