using System.Security.Claims;
using EPM.Application.Abstractions;
using EPM.Domain.Identity;
using Microsoft.AspNetCore.Http;

namespace EPM.Infrastructure.Identity;

/// <summary>
/// Reads the caller's identity out of the validated token on the current request.
/// </summary>
/// <remarks>
/// Everything here comes from claims the authentication middleware has already verified, so
/// there is no trust decision left to make — by the time this runs, a forged token would have
/// been rejected. Outside a request (background work, the seeder) every property is null and
/// IsAuthenticated is false.
/// </remarks>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int? UserId => ReadInt(AppClaimTypes.UserId);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
                            ?? Principal?.FindFirstValue("email");

    public UserRole? Role
    {
        get
        {
            var roleName = Principal?.FindFirstValue(ClaimTypes.Role);

            return roleName switch
            {
                RoleNames.Admin => UserRole.Admin,
                RoleNames.Manager => UserRole.Manager,
                RoleNames.User => UserRole.User,
                // An unrecognised role name means the token is valid but carries a role this
                // build does not know. Treated as "no role" so it fails every policy rather
                // than accidentally satisfying one.
                _ => null,
            };
        }
    }

    public int? EmployeeId => ReadInt(AppClaimTypes.EmployeeId);

    private int? ReadInt(string claimType)
    {
        var raw = Principal?.FindFirstValue(claimType);

        return int.TryParse(raw, out var value) ? value : null;
    }
}
