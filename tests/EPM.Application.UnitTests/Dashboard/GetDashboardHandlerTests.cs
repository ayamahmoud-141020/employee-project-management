using EPM.Application.Features.Dashboard.GetDashboard;
using EPM.Application.UnitTests.Infrastructure;
using EPM.Domain.Projects;
using FluentAssertions;

namespace EPM.Application.UnitTests.Dashboard;

public sealed class GetDashboardHandlerTests : IAsyncLifetime
{
    private readonly SliceTestHarness _harness = new();

    private GetDashboardHandler Handler => new(_harness.Db);

    public async Task InitializeAsync()
    {
        var engineering = await _harness.GivenDepartmentAsync("Engineering");
        await _harness.GivenDepartmentAsync("Empty Department");

        await _harness.GivenEmployeeAsync(engineering.Id, "one@epm.local");
        await _harness.GivenEmployeeAsync(engineering.Id, "two@epm.local");
        await _harness.GivenEmployeeAsync(engineering.Id, "three@epm.local", active: false);

        await AddProjectAsync("Active One", ProjectStatus.Active);
        await AddProjectAsync("Active Two", ProjectStatus.Active);
        await AddProjectAsync("Planned", ProjectStatus.Planning);

        _harness.DetachAll();
    }

    [Fact]
    public async Task Headline_counts_separate_total_from_active()
    {
        var result = await Handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        result.Value.TotalEmployees.Should().Be(3);
        result.Value.ActiveEmployees.Should().Be(2);
        result.Value.InactiveEmployees.Should().Be(1);
        result.Value.TotalDepartments.Should().Be(2);
        result.Value.TotalProjects.Should().Be(3);
        result.Value.ActiveProjects.Should().Be(2);
    }

    [Fact]
    public async Task Departments_with_nobody_in_them_still_appear()
    {
        // Grouping from the employee side would drop the empty department entirely, and a
        // chart that silently omits categories is worse than one showing a zero.
        var result = await Handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        result.Value.EmployeesByDepartment.Should().HaveCount(2);
        result.Value.EmployeesByDepartment
            .Single(department => department.DepartmentName == "Empty Department")
            .EmployeeCount.Should().Be(0);
        result.Value.EmployeesByDepartment
            .Single(department => department.DepartmentName == "Engineering")
            .ActiveEmployeeCount.Should().Be(2);
    }

    [Fact]
    public async Task Every_project_status_is_reported_including_the_unused_ones()
    {
        var result = await Handler.Handle(new GetDashboardQuery(), CancellationToken.None);

        result.Value.ProjectsByStatus.Should().HaveCount(Enum.GetValues<ProjectStatus>().Length);
        result.Value.ProjectsByStatus.Single(s => s.Status == ProjectStatus.Active).Count.Should().Be(2);
        result.Value.ProjectsByStatus.Single(s => s.Status == ProjectStatus.Cancelled).Count.Should().Be(0);
    }

    private async Task AddProjectAsync(string name, ProjectStatus status)
    {
        var project = Project.Create(
            name, null, _harness.Clock.Today.AddMonths(-1), _harness.Clock.Today.AddMonths(6), status).Value;

        _harness.Context.Projects.Add(project);
        await _harness.Context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _harness.Dispose();

        return Task.CompletedTask;
    }
}
