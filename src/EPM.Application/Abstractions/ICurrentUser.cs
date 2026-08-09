using EPM.Domain.Identity;

namespace EPM.Application.Abstractions;

/// <summary>
/// Who is making the current request, read from the validated token.
/// </summary>
/// <remarks>
/// Handlers use this instead of taking a user id as a command parameter. A caller-supplied id
/// is something the caller can lie about; this one comes from a signed token. It is what
/// makes "GET /api/me/assignments returns only my assignments" enforceable.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    int? UserId { get; }

    string? Email { get; }

    UserRole? Role { get; }

    /// <summary>
    /// The employee record linked to this account, if any. Null for accounts with no employee
    /// row (a service admin), which is why "my assignments" has to handle the empty case.
    /// </summary>
    int? EmployeeId { get; }
}
