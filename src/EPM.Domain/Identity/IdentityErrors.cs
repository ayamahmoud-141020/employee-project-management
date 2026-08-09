using EPM.Domain.Abstractions;

namespace EPM.Domain.Identity;

public static class IdentityErrors
{
    /// <summary>
    /// Used for both "no such account" and "wrong password" on purpose. Distinguishing them
    /// tells an attacker which addresses are registered.
    /// </summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Email or password is incorrect.");

    public static readonly Error AccountDisabled =
        Error.Forbidden("Auth.AccountDisabled", "This account has been disabled.");

    public static readonly Error PasswordLoginNotAvailable =
        Error.Forbidden(
            "Auth.PasswordLoginNotAvailable",
            "This account signs in through the identity provider and has no password.");

    public static readonly Error RefreshTokenInvalid =
        Error.Unauthorized("Auth.RefreshTokenInvalid", "Refresh token is invalid, expired or already used.");

    public static readonly Error EmailAlreadyRegistered =
        Error.Conflict("Auth.EmailAlreadyRegistered", "An account with this email already exists.");

    public static readonly Error PasswordTooWeak =
        Error.Validation(
            "Auth.PasswordTooWeak",
            $"Password must be at least {AppUser.MinPasswordLength} characters and mix letters and digits.");
}
