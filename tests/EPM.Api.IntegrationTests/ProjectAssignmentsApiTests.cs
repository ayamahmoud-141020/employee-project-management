using System.Net;
using System.Net.Http.Json;
using EPM.Api.IntegrationTests.Infrastructure;
using EPM.Application.Features.Dashboard.GetDashboard;
using EPM.Application.Features.ProjectAssignments.Contracts;
using EPM.Application.Features.Projects.Contracts;
using FluentAssertions;

namespace EPM.Api.IntegrationTests;

/// <summary>
/// The assignment rules and the dashboard, over real HTTP against real SQL Server.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProjectAssignmentsApiTests(ApiFactory factory) : IAsyncLifetime
{
    private HttpClient _admin = null!;
    private int _adaId;
    private int _teslaId;

    public async Task InitializeAsync()
    {
        await DatabaseFixtures.EnsureSeededAsync(factory);
        _admin = await factory.CreateClientAsAsync(DatabaseFixtures.AdminEmail, DatabaseFixtures.Password);
        _adaId = await DatabaseFixtures.GetEmployeeIdAsync(factory, "ada.lovelace@epm.local");
        _teslaId = await DatabaseFixtures.GetEmployeeIdAsync(factory, "nikola.tesla@epm.local");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_employee_can_be_assigned_listed_and_removed()
    {
        var projectId = await CreateProjectAsync();

        var assigned = await AssignAsync(projectId, _adaId, "Tech Lead", "2026-06-01", 60);
        assigned.StatusCode.Should().Be(HttpStatusCode.Created);

        var team = await _admin.GetAsync($"/api/projects/{projectId}/employees");
        team.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await Read<IReadOnlyList<ProjectAssignmentResponse>>(team);
        members.Data.Should().ContainSingle();
        members.Data![0].EmployeeName.Should().Be("Ada Lovelace");
        members.Data[0].AllocationPercentage.Should().Be(60);

        var removed = await _admin.DeleteAsync($"/api/projects/{projectId}/employees/{_adaId}");
        removed.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Read<IReadOnlyList<ProjectAssignmentResponse>>(
            await _admin.GetAsync($"/api/projects/{projectId}/employees"))).Data.Should().BeEmpty();
    }

    [Fact]
    public async Task An_inactive_employee_cannot_be_assigned()
    {
        var projectId = await CreateProjectAsync();

        var response = await AssignAsync(projectId, _teslaId, "Engineer", "2026-06-01", 50);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Read<object>(response)).Code.Should().Be("Assignment.EmployeeInactive");
    }

    [Fact]
    public async Task The_same_employee_cannot_be_assigned_twice()
    {
        var projectId = await CreateProjectAsync();
        await AssignAsync(projectId, _adaId, "Tech Lead", "2026-06-01", 60);

        var second = await AssignAsync(projectId, _adaId, "Reviewer", "2026-06-01", 10);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Read<object>(second)).Code.Should().Be("Assignment.Duplicate");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Allocation_outside_one_to_hundred_is_rejected(int percentage)
    {
        var projectId = await CreateProjectAsync();

        var response = await AssignAsync(projectId, _adaId, "Engineer", "2026-06-01", percentage);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_assigned_date_outside_the_project_schedule_is_rejected()
    {
        var projectId = await CreateProjectAsync();

        var response = await AssignAsync(projectId, _adaId, "Engineer", "2020-01-01", 50);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Read<object>(response)).Code.Should().Be("Assignment.DateOutsideProjectSchedule");
    }

    [Fact]
    public async Task Deactivating_an_employee_takes_them_off_their_projects()
    {
        // The other half of "inactive employees cannot be assigned": existing allocations have
        // to be released too, or a deactivated person keeps holding capacity.
        var projectId = await CreateProjectAsync();
        var departmentId = await DatabaseFixtures.GetDepartmentIdAsync(factory);

        var created = await _admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Temporary",
            lastName = "Assignee",
            email = $"temp.{Guid.NewGuid():N}@epm.local",
            jobTitle = "Engineer",
            departmentId,
            hireDate = "2024-01-01",
        });

        var employeeId = (await Read<EmployeeId>(created)).Data!.Id;
        (await AssignAsync(projectId, employeeId, "Engineer", "2026-06-01", 40)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        await _admin.DeleteAsync($"/api/employees/{employeeId}");

        var team = await Read<IReadOnlyList<ProjectAssignmentResponse>>(
            await _admin.GetAsync($"/api/projects/{projectId}/employees"));

        team.Data.Should().NotContain(member => member.EmployeeId == employeeId);
    }

    [Fact]
    public async Task Shrinking_a_schedule_under_an_assignment_is_refused()
    {
        var projectId = await CreateProjectAsync();
        await AssignAsync(projectId, _adaId, "Tech Lead", "2026-11-01", 50);

        var response = await _admin.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            name = $"Shrunk {Guid.NewGuid():N}",
            startDate = "2026-01-01",
            endDate = "2026-06-30",
            status = "Active",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Read<object>(response)).Code.Should().Be("Project.ScheduleConflictsWithAssignments");
    }

    [Fact]
    public async Task A_department_with_employees_cannot_be_deleted()
    {
        var departmentId = await DatabaseFixtures.GetDepartmentIdAsync(factory);

        var response = await _admin.DeleteAsync($"/api/departments/{departmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Read<object>(response)).Code.Should().StartWith("Department.HasActiveEmployees");
    }

    [Fact]
    public async Task The_dashboard_reports_consistent_totals()
    {
        var response = await _admin.GetAsync("/api/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dashboard = (await Read<DashboardResponse>(response)).Data!;

        dashboard.TotalEmployees.Should().Be(dashboard.ActiveEmployees + dashboard.InactiveEmployees);
        dashboard.ActiveEmployees.Should().BeLessThanOrEqualTo(dashboard.TotalEmployees);
        dashboard.ActiveProjects.Should().BeLessThanOrEqualTo(dashboard.TotalProjects);
        dashboard.EmployeesByDepartment.Sum(d => d.EmployeeCount).Should().Be(dashboard.TotalEmployees);
        dashboard.ProjectsByStatus.Sum(s => s.Count).Should().Be(dashboard.TotalProjects);
        dashboard.ProjectsByStatus.Should().HaveCount(4, "every status is reported, even the empty ones");
    }

    [Fact]
    public async Task My_assignments_is_scoped_to_the_calling_account()
    {
        // The User fixture has no linked employee record, so it must see nothing at all —
        // the endpoint takes no id, which is what makes that guarantee hold.
        var user = await factory.CreateClientAsAsync(DatabaseFixtures.UserEmail, DatabaseFixtures.Password);

        var response = await user.GetAsync("/api/me/assignments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Read<IReadOnlyList<object>>(response)).Data.Should().BeEmpty();
    }

    private async Task<int> CreateProjectAsync()
    {
        var response = await _admin.PostAsJsonAsync("/api/projects", new
        {
            name = $"Assignment probe {Guid.NewGuid():N}",
            description = "Created by an integration test.",
            startDate = "2026-01-01",
            endDate = "2026-12-31",
            status = "Active",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await Read<ProjectResponse>(response)).Data!.Id;
    }

    private Task<HttpResponseMessage> AssignAsync(
        int projectId, int employeeId, string role, string assignedDate, int allocationPercentage) =>
        _admin.PostAsJsonAsync($"/api/projects/{projectId}/employees", new
        {
            employeeId,
            role,
            assignedDate,
            allocationPercentage,
        });

    private static async Task<ApiEnvelope<T>> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(ApiFactory.Json))!;

    private sealed record EmployeeId(int Id);
}
