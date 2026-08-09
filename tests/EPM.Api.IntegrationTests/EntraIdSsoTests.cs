using System.Net;
using System.Net.Http.Json;
using EPM.Api.IntegrationTests.Infrastructure;
using EPM.Domain.Identity;
using EPM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EPM.Api.IntegrationTests;

/// <summary>
/// End-to-end verification of the Entra ID single sign-on path, against a locally hosted
/// OpenID Connect issuer.
/// </summary>
/// <remarks>
/// Every request here carries a genuinely signed JWT. The API performs its real discovery
/// fetch, downloads the signing keys, and validates signature, issuer, audience and lifetime
/// before the claims transformation runs. Nothing in the application is stubbed.
///
/// What these tests do <b>not</b> cover, because it is not code in this repository: Microsoft's
/// own token issuance, the interactive sign-in UI, and the app-registration settings in the
/// Azure portal.
/// </remarks>
[Collection(EntraIdCollection.Name)]
public sealed class EntraIdSsoTests(EntraIdApiFactory factory)
{
    [Fact]
    public async Task A_valid_Entra_token_is_accepted()
    {
        var client = factory.CreateSsoClient(
            NewObjectId(), "viewer@contoso.com", "Vera Viewer", "EPM.User");

        var response = await client.GetAsync("/api/employees?pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_token_signed_with_an_untrusted_key_is_rejected()
    {
        // Proves signatures are verified against the published JWKS rather than merely parsed.
        // Without this, "SSO works" would only mean "the API can read base64".
        var forged = factory.IdentityProvider.IssueTokenSignedWithUntrustedKey(
            EntraIdApiFactory.ClientId, NewObjectId());

        var response = await factory.CreateClientWithToken(forged).GetAsync("/api/employees");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_for_the_wrong_audience_is_rejected()
    {
        var wrongAudience = factory.IdentityProvider.IssueToken(
            audience: "api://some-other-application",
            objectId: NewObjectId(),
            email: "someone@contoso.com",
            name: "Someone",
            appRoles: ["EPM.Admin"]);

        var response = await factory.CreateClientWithToken(wrongAudience).GetAsync("/api/employees");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_from_an_unexpected_issuer_is_rejected()
    {
        // Correctly signed by the trusted key, but claiming a different tenant. This is what
        // keeps the issuer check honest after AadIssuerValidator was swapped out for the test
        // host — see EntraIdApiFactory.
        var foreign = factory.IdentityProvider.IssueTokenWithForeignIssuer(
            EntraIdApiFactory.ClientId, NewObjectId());

        var response = await factory.CreateClientWithToken(foreign).GetAsync("/api/employees");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var expired = factory.IdentityProvider.IssueToken(
            EntraIdApiFactory.ClientId,
            NewObjectId(),
            "expired@contoso.com",
            "Ex Pired",
            ["EPM.Admin"],
            expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var response = await factory.CreateClientWithToken(expired).GetAsync("/api/employees");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task First_sign_in_provisions_a_local_account_keyed_on_the_object_id()
    {
        var objectId = NewObjectId();
        var email = $"jit.{Guid.NewGuid():N}@contoso.com";

        var client = factory.CreateSsoClient(objectId, email, "Jit Provisioned", "EPM.Manager");

        (await client.GetAsync("/api/dashboard")).StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await FindByObjectIdAsync(objectId);

        user.Should().NotBeNull("the first SSO sign-in must create a local account");
        user!.Email.Value.Should().Be(email);
        user.DisplayName.Should().Be("Jit Provisioned");
        user.Role.Should().Be(UserRole.Manager);
        user.PasswordHash.Should().BeNull("an SSO account has no password");
    }

    [Fact]
    public async Task Signing_in_twice_does_not_create_a_second_account()
    {
        var objectId = NewObjectId();
        var email = $"repeat.{Guid.NewGuid():N}@contoso.com";

        var first = factory.CreateSsoClient(objectId, email, "Repeat User", "EPM.User");
        await first.GetAsync("/api/dashboard");

        var second = factory.CreateSsoClient(objectId, email, "Repeat User", "EPM.User");
        await second.GetAsync("/api/dashboard");

        var count = await CountByObjectIdAsync(objectId);

        count.Should().Be(1, "provisioning is keyed on the stable oid claim, so it is idempotent");
    }

    [Fact]
    public async Task An_existing_password_account_is_linked_rather_than_duplicated()
    {
        // Somebody who already had a local login and now arrives through SSO must end up as one
        // person, not two rows with the same address.
        var email = $"existing.{Guid.NewGuid():N}@contoso.com";
        var localUserId = await GivenLocalAccountAsync(email, UserRole.Admin);

        var objectId = NewObjectId();
        var client = factory.CreateSsoClient(objectId, email, "Existing Person", "EPM.Admin");

        (await client.GetAsync("/api/dashboard")).StatusCode.Should().Be(HttpStatusCode.OK);

        var byEmail = await CountByEmailAsync(email);
        byEmail.Should().Be(1, "the SSO identity must attach to the existing account");

        var user = await FindByObjectIdAsync(objectId);
        user!.Id.Should().Be(localUserId, "it must be the same row, now carrying the external id");
    }

    [Theory]
    [InlineData("EPM.Admin", UserRole.Admin)]
    [InlineData("EPM.Manager", UserRole.Manager)]
    [InlineData("EPM.User", UserRole.User)]
    public async Task App_roles_map_onto_the_local_role(string appRole, UserRole expected)
    {
        var objectId = NewObjectId();

        var client = factory.CreateSsoClient(
            objectId, $"role.{Guid.NewGuid():N}@contoso.com", "Role Mapped", appRole);

        await client.GetAsync("/api/dashboard");

        (await FindByObjectIdAsync(objectId))!.Role.Should().Be(expected);
    }

    [Fact]
    public async Task When_several_app_roles_are_assigned_the_most_privileged_one_wins()
    {
        // Entra sends every assigned role, in no guaranteed order. Taking the first would make
        // an admin's effective permissions depend on array ordering.
        var objectId = NewObjectId();

        var client = factory.CreateSsoClient(
            objectId, $"multi.{Guid.NewGuid():N}@contoso.com", "Multi Role",
            "EPM.User", "EPM.Admin", "EPM.Manager");

        await client.GetAsync("/api/dashboard");

        (await FindByObjectIdAsync(objectId))!.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task A_user_with_no_recognised_app_role_falls_back_to_the_least_privileged_one()
    {
        // A delegated token from a user who was never assigned an app role: it carries a scope
        // (the client requested one during sign-in) but no `roles`. This is the case
        // EntraId:DefaultRole exists for.
        var objectId = NewObjectId();

        var client = factory.CreateSsoClient(
            objectId, $"norole.{Guid.NewGuid():N}@contoso.com", "No Role");

        var response = await client.GetAsync("/api/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the fallback role can still read");

        var user = await FindByObjectIdAsync(objectId);
        user.Should().NotBeNull("a first-time SSO user is provisioned even with no app role");

        user!.Role.Should().Be(
            UserRole.User, "an unmapped identity should be able to look around, not administer");
    }

    [Fact]
    public async Task A_token_with_neither_scope_nor_role_is_rejected()
    {
        // Microsoft.Identity.Web refuses a bearer token that carries neither `scp` nor `roles`,
        // before any application code runs. Worth pinning: it means EntraId:DefaultRole can
        // never rescue such a token — the request is already a 401 by then.
        var token = factory.IdentityProvider.IssueToken(
            EntraIdApiFactory.ClientId,
            NewObjectId(),
            "noscope@contoso.com",
            "No Scope",
            appRoles: null,
            expiresAt: null,
            scope: null);

        var response = await factory.CreateClientWithToken(token).GetAsync("/api/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_role_changed_in_the_directory_takes_effect_on_the_next_sign_in()
    {
        var objectId = NewObjectId();
        var email = $"promoted.{Guid.NewGuid():N}@contoso.com";

        var asUser = factory.CreateSsoClient(objectId, email, "Promoted Person", "EPM.User");
        await asUser.GetAsync("/api/dashboard");
        (await FindByObjectIdAsync(objectId))!.Role.Should().Be(UserRole.User);

        var asAdmin = factory.CreateSsoClient(objectId, email, "Promoted Person", "EPM.Admin");
        await asAdmin.GetAsync("/api/dashboard");

        (await FindByObjectIdAsync(objectId))!.Role.Should().Be(
            UserRole.Admin, "Entra is the source of truth for role while SSO is enabled");
    }

    [Fact]
    public async Task An_SSO_admin_can_do_what_a_local_admin_can()
    {
        // The point of the claims transformation: an SSO identity has to resolve through the
        // exact same policies as a locally issued token, not a parallel set of rules.
        var admin = factory.CreateSsoClient(
            NewObjectId(), $"ssoadmin.{Guid.NewGuid():N}@contoso.com", "SSO Admin", "EPM.Admin");

        var departmentId = await GivenDepartmentAsync();

        var response = await admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Created",
            lastName = "ViaSso",
            email = $"created.{Guid.NewGuid():N}@epm.local",
            jobTitle = "Engineer",
            departmentId,
            hireDate = "2024-01-01",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_SSO_user_is_refused_what_the_User_role_cannot_do()
    {
        var user = factory.CreateSsoClient(
            NewObjectId(), $"ssouser.{Guid.NewGuid():N}@contoso.com", "SSO User", "EPM.User");

        var departmentId = await GivenDepartmentAsync();

        var response = await user.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Should",
            lastName = "Fail",
            email = $"fail.{Guid.NewGuid():N}@epm.local",
            jobTitle = "Engineer",
            departmentId,
            hireDate = "2024-01-01",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Local_password_login_still_works_while_SSO_is_enabled()
    {
        // Both schemes are registered side by side and selected per request by issuer. If
        // enabling SSO broke local login, this is where it would show.
        var email = $"local.{Guid.NewGuid():N}@epm.local";
        await GivenLocalAccountAsync(email, UserRole.Admin, "Local#Password123");

        var anonymous = factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync(
            "/api/auth/login", new { email, password = "Local#Password123" });

        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await login.Content.ReadFromJsonAsync<ApiEnvelope<TokenPayload>>(ApiFactory.Json);
        var client = factory.CreateClientWithToken(payload!.Data!.AccessToken);

        (await client.GetAsync("/api/employees?pageSize=1")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_deactivated_SSO_account_loses_access_even_with_a_valid_token()
    {
        var objectId = NewObjectId();
        var email = $"disabled.{Guid.NewGuid():N}@contoso.com";

        var client = factory.CreateSsoClient(objectId, email, "To Be Disabled", "EPM.Admin");
        await client.GetAsync("/api/dashboard");

        await DeactivateAsync(objectId);

        // Same valid token, but the local account is now disabled: authentication still
        // succeeds, yet no role claim is attached, so every policy denies it.
        var afterDisable = factory.CreateSsoClient(objectId, email, "To Be Disabled", "EPM.Admin");
        var response = await afterDisable.GetAsync("/api/employees");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string NewObjectId() => Guid.NewGuid().ToString();

    private async Task<AppUser?> FindByObjectIdAsync(string objectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ExternalObjectId == objectId);
    }

    private async Task<int> CountByObjectIdAsync(string objectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Users.CountAsync(u => u.ExternalObjectId == objectId);
    }

    private async Task<int> CountByEmailAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Users.CountAsync(u => u.Email.Value == email);
    }

    private async Task<int> GivenLocalAccountAsync(string email, UserRole role, string password = "Seeded#Password1")
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<EPM.Application.Abstractions.IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<EPM.Application.Abstractions.IDateTimeProvider>();

        var user = AppUser.CreateLocal(email, "Existing Person", hasher.Hash(password), role, null, clock.UtcNow).Value;
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    private async Task DeactivateAsync(string objectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // No domain method disables an account, so the test writes the column directly — the
        // production path for this is an administrative action outside the current scope.
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE Users SET IsActive = 0 WHERE ExternalObjectId = {0}", objectId);
    }

    private async Task<int> GivenDepartmentAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await context.Departments.Select(d => d.Id).FirstOrDefaultAsync();

        if (existing != 0)
        {
            return existing;
        }

        var department = EPM.Domain.Departments.Department.Create("Engineering", null).Value;
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        return department.Id;
    }

    private sealed record TokenPayload(string AccessToken);
}
