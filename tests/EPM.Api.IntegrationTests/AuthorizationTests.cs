using System.Net;
using System.Net.Http.Json;
using EPM.Api.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EPM.Api.IntegrationTests;

/// <summary>
/// Proves the role matrix from the spec is enforced at the API, not just documented.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationTests(ApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => DatabaseFixtures.EnsureSeededAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/api/employees")]
    [InlineData("/api/departments")]
    [InlineData("/api/projects")]
    [InlineData("/api/dashboard")]
    [InlineData("/api/me/assignments")]
    public async Task Protected_endpoints_reject_anonymous_callers(string route)
    {
        var response = await factory.CreateAnonymousClient().GetAsync(route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_and_login_stay_open()
    {
        var client = factory.CreateAnonymousClient();

        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Wrong password, but the endpoint itself must be reachable — a 401 here means the
        // credentials were rejected, which is the point. A 404 would mean it is not mapped.
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@epm.local", password = "wrong" });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(DatabaseFixtures.AdminEmail, HttpStatusCode.Created)]
    [InlineData(DatabaseFixtures.ManagerEmail, HttpStatusCode.Forbidden)]
    [InlineData(DatabaseFixtures.UserEmail, HttpStatusCode.Forbidden)]
    public async Task Only_admins_can_create_employees(string email, HttpStatusCode expected)
    {
        var client = await factory.CreateClientAsAsync(email, DatabaseFixtures.Password);
        var departmentId = await DatabaseFixtures.GetDepartmentIdAsync(factory);

        var response = await client.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Role",
            lastName = "Probe",
            email = $"role.probe.{Guid.NewGuid():N}@epm.local",
            jobTitle = "Engineer",
            departmentId,
            hireDate = "2024-01-01",
        });

        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData(DatabaseFixtures.AdminEmail, HttpStatusCode.Created)]
    [InlineData(DatabaseFixtures.ManagerEmail, HttpStatusCode.Created)]
    [InlineData(DatabaseFixtures.UserEmail, HttpStatusCode.Forbidden)]
    public async Task Admins_and_managers_can_create_projects(string email, HttpStatusCode expected)
    {
        var client = await factory.CreateClientAsAsync(email, DatabaseFixtures.Password);

        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            name = $"Role probe {Guid.NewGuid():N}",
            startDate = "2026-01-01",
            endDate = "2026-12-31",
            status = "Planning",
        });

        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData(DatabaseFixtures.AdminEmail)]
    [InlineData(DatabaseFixtures.ManagerEmail)]
    [InlineData(DatabaseFixtures.UserEmail)]
    public async Task Every_role_can_read_the_directory(string email)
    {
        var client = await factory.CreateClientAsAsync(email, DatabaseFixtures.Password);

        (await client.GetAsync("/api/employees?pageSize=1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/projects?pageSize=1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/dashboard")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_admins_can_manage_departments()
    {
        var manager = await factory.CreateClientAsAsync(DatabaseFixtures.ManagerEmail, DatabaseFixtures.Password);

        var response = await manager.PostAsJsonAsync("/api/departments", new { name = $"Probe {Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_garbled_token_is_rejected()
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer not-a-real-token");

        (await client.GetAsync("/api/employees")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
