using System.Security.Claims;
using EPM.Application.Abstractions;
using EPM.Domain.Employees;
using EPM.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EPM.Infrastructure.Identity;

/// <summary>
/// Turns an Entra ID token into the same shape as a locally issued one.
/// </summary>
/// <remarks>
/// Without this, an SSO user would authenticate successfully and then fail every policy: the
/// token has Entra's app roles and object id, but no local user id, no employee link, and no
/// role claim in the form the policies expect. This runs after the token is validated and
/// fills that gap, provisioning a local Users row the first time someone signs in so both
/// login paths converge on one authorization model.
///
/// Registered as a singleton by the framework, so the scoped DbContext is resolved per call
/// from a scope rather than injected — the usual captive-dependency trap.
/// </remarks>
public sealed class EntraIdClaimsTransformation(
    IServiceScopeFactory scopeFactory,
    IOptions<EntraIdOptions> options,
    ILogger<EntraIdClaimsTransformation> logger)
    : IClaimsTransformation
{
    private readonly EntraIdOptions _options = options.Value;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var objectId = principal.FindFirstValue(AppClaimTypes.ObjectId)
                       ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

        // No `oid` means this is not an Entra token — a locally issued one already carries
        // everything it needs, so there is nothing to do.
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return principal;
        }

        // IClaimsTransformation can run more than once per request. Bailing out if the local
        // claims are already attached keeps that from duplicating them or re-hitting the DB.
        if (principal.HasClaim(claim => claim.Type == AppClaimTypes.UserId))
        {
            return principal;
        }

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var email = principal.FindFirstValue("preferred_username")
                    ?? principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("upn");

        var displayName = principal.FindFirstValue("name") ?? email ?? objectId;
        var role = ResolveRole(principal);

        var user = await context.Users.FirstOrDefaultAsync(u => u.ExternalObjectId == objectId);

        if (user is null)
        {
            // Someone who already had a password account and is now arriving via SSO. Linking
            // rather than creating avoids two rows — and two identities — for one person.
            // Normalised outside the predicate and compared on .Value — Email is an owned
            // type, so EF can translate member access but not whole-object equality, and a
            // call to Email.Create inside an expression tree is not translatable at all.
            var normalisedEmail = Email.Create(email);

            if (normalisedEmail.IsSuccess)
            {
                var target = normalisedEmail.Value.Value;
                user = await context.Users.FirstOrDefaultAsync(u => u.Email.Value == target);
                user?.LinkExternalIdentity(objectId);
            }
        }

        if (user is null)
        {
            var created = AppUser.CreateFromExternalIdentity(
                objectId, email, displayName, role, employeeId: null, clock.UtcNow);

            if (created.IsFailure)
            {
                // Usually a token with no usable email claim. The principal is left as-is:
                // authenticated, but carrying no local role, so every policy denies it.
                logger.LogWarning(
                    "Could not provision an account for Entra object id {ObjectId}: {ErrorCode}",
                    objectId,
                    created.Error.Code);

                return principal;
            }

            user = created.Value;
            context.Users.Add(user);

            logger.LogInformation("Provisioned local account for Entra user {Email}", user.Email.Value);
        }
        else if (user.Role != role)
        {
            // Entra is the source of truth for role while SSO is on, so a role changed in the
            // directory takes effect on the user's next sign-in without a manual edit here.
            user.ChangeRole(role);
        }

        if (!user.IsActive)
        {
            return principal;
        }

        await context.SaveChangesAsync();

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AppClaimTypes.UserId, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, RoleNames.From(user.Role)));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email.Value));

        if (user.EmployeeId.HasValue)
        {
            identity.AddClaim(new Claim(AppClaimTypes.EmployeeId, user.EmployeeId.Value.ToString()));
        }

        principal.AddIdentity(identity);

        return principal;
    }

    /// <summary>
    /// Maps Entra app roles onto the local role enum. Entra sends every assigned role, so the
    /// most privileged one wins rather than whichever happens to come first in the array.
    /// </summary>
    private UserRole ResolveRole(ClaimsPrincipal principal)
    {
        var appRoles = principal.FindAll("roles")
            .Concat(principal.FindAll(ClaimTypes.Role))
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (appRoles.Contains(EntraAppRoles.Admin))
        {
            return UserRole.Admin;
        }

        if (appRoles.Contains(EntraAppRoles.Manager))
        {
            return UserRole.Manager;
        }

        if (appRoles.Contains(EntraAppRoles.User))
        {
            return UserRole.User;
        }

        return _options.DefaultRole switch
        {
            RoleNames.Admin => UserRole.Admin,
            RoleNames.Manager => UserRole.Manager,
            _ => UserRole.User,
        };
    }
}

/// <summary>App role values as declared in the Entra app registration manifest.</summary>
public static class EntraAppRoles
{
    public const string Admin = "EPM.Admin";
    public const string Manager = "EPM.Manager";
    public const string User = "EPM.User";
}
