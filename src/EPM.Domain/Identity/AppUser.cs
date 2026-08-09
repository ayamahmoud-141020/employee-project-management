using EPM.Domain.Abstractions;
using EPM.Domain.Employees;

namespace EPM.Domain.Identity;

/// <summary>
/// A login account.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="Employee"/> rather than bolting a password onto it. The two
/// have genuinely different lifecycles: most employees never need portal access, and some
/// accounts (a service admin, an external contractor's manager) have no employee record at
/// all. <see cref="EmployeeId"/> is the optional bridge, and it is what lets a User-role
/// account see "my assignments".
///
/// The same account can authenticate locally or through Entra ID. An SSO-provisioned user
/// has an <see cref="ExternalObjectId"/> and no password hash; a local user is the reverse.
/// </remarks>
public sealed class AppUser : AggregateRoot
{
    public const int MinPasswordLength = 8;

    private readonly List<RefreshToken> _refreshTokens = [];

    private AppUser()
    {
    }

    public Email Email { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    /// <summary>Null for accounts that only sign in through the identity provider.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>
    /// The `oid` claim from Entra ID. Stable per user per tenant, unlike email, which is why
    /// it — not the address — is the key used to match a returning SSO user.
    /// </summary>
    public string? ExternalObjectId { get; private set; }

    public UserRole Role { get; private set; }

    public int? EmployeeId { get; private set; }

    public Employee? Employee { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static Result<AppUser> CreateLocal(
        string? email,
        string displayName,
        string passwordHash,
        UserRole role,
        int? employeeId,
        DateTime utcNow)
    {
        var emailResult = Email.Create(email);

        if (emailResult.IsFailure)
        {
            return Result.Failure<AppUser>(emailResult.Error);
        }

        return new AppUser
        {
            Email = emailResult.Value,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? emailResult.Value.Value : displayName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            EmployeeId = employeeId,
            IsActive = true,
            CreatedAtUtc = utcNow,
        };
    }

    /// <summary>
    /// Provisions an account the first time someone signs in through the identity provider.
    /// </summary>
    public static Result<AppUser> CreateFromExternalIdentity(
        string externalObjectId,
        string? email,
        string displayName,
        UserRole role,
        int? employeeId,
        DateTime utcNow)
    {
        var emailResult = Email.Create(email);

        if (emailResult.IsFailure)
        {
            return Result.Failure<AppUser>(emailResult.Error);
        }

        return new AppUser
        {
            Email = emailResult.Value,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? emailResult.Value.Value : displayName.Trim(),
            ExternalObjectId = externalObjectId,
            PasswordHash = null,
            Role = role,
            EmployeeId = employeeId,
            IsActive = true,
            CreatedAtUtc = utcNow,
        };
    }

    /// <summary>
    /// Links an existing local account to an identity-provider identity, so someone who
    /// already had a password can start using SSO without ending up with two accounts.
    /// </summary>
    public void LinkExternalIdentity(string externalObjectId) => ExternalObjectId ??= externalObjectId;

    public void ChangeRole(UserRole role) => Role = role;

    public void LinkToEmployee(int employeeId) => EmployeeId = employeeId;

    public RefreshToken IssueRefreshToken(string token, DateTime expiresAtUtc, DateTime utcNow)
    {
        var refreshToken = RefreshToken.Issue(token, expiresAtUtc, utcNow);
        _refreshTokens.Add(refreshToken);

        return refreshToken;
    }

    /// <summary>
    /// Rotates a refresh token: the presented one is revoked and a replacement issued, so a
    /// stolen token stops working the moment the legitimate client next refreshes.
    /// </summary>
    public Result<RefreshToken> RotateRefreshToken(
        string presentedToken,
        string replacementToken,
        DateTime replacementExpiresAtUtc,
        DateTime utcNow)
    {
        var existing = _refreshTokens.SingleOrDefault(t => t.Token == presentedToken);

        if (existing is null || !existing.IsUsable(utcNow))
        {
            return Result.Failure<RefreshToken>(IdentityErrors.RefreshTokenInvalid);
        }

        existing.Revoke(utcNow);

        return IssueRefreshToken(replacementToken, replacementExpiresAtUtc, utcNow);
    }

    public void RevokeAllRefreshTokens(DateTime utcNow)
    {
        foreach (var token in _refreshTokens.Where(t => t.IsUsable(utcNow)))
        {
            token.Revoke(utcNow);
        }
    }

    /// <summary>
    /// Checks the account is in a state that permits password sign-in. The password itself is
    /// verified by infrastructure — hashing algorithms are not a domain concern.
    /// </summary>
    public Result EnsureCanSignInWithPassword()
    {
        if (!IsActive)
        {
            return Result.Failure(IdentityErrors.AccountDisabled);
        }

        return PasswordHash is null
            ? Result.Failure(IdentityErrors.PasswordLoginNotAvailable)
            : Result.Success();
    }
}
