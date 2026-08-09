using EPM.Application.Features.ProjectAssignments.GetProjectEmployees;
using EPM.Application.UnitTests.Infrastructure;
using EPM.Domain.Projects;
using FluentAssertions;

namespace EPM.Application.UnitTests.ProjectAssignments;

/// <summary>
/// Regression cover for value-object mapping.
/// </summary>
/// <remarks>
/// This list sorts by allocation, which is a value object. When Allocation was mapped with a
/// value converter instead of as an owned type, the ORDER BY could not be translated and the
/// endpoint returned a 500 — but only against a real relational provider. That is exactly the
/// class of bug the EF InMemory provider hides, and why these tests run on SQLite.
/// </remarks>
public sealed class GetProjectEmployeesHandlerTests : IDisposable
{
    private readonly SliceTestHarness _harness = new();

    private GetProjectEmployeesHandler Handler => new(_harness.Db);

    [Fact]
    public async Task The_team_comes_back_sorted_by_allocation_descending()
    {
        var department = await _harness.GivenDepartmentAsync();
        var project = await _harness.GivenProjectAsync();

        var junior = await _harness.GivenEmployeeAsync(department.Id, "junior@epm.local");
        var senior = await _harness.GivenEmployeeAsync(department.Id, "senior@epm.local");

        project.AssignEmployee(junior.Id, true, "Engineer", _harness.Clock.Today, 20);
        project.AssignEmployee(senior.Id, true, "Tech Lead", _harness.Clock.Today, 80);
        await _harness.Context.SaveChangesAsync();
        _harness.DetachAll();

        var result = await Handler.Handle(new GetProjectEmployeesQuery(project.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(member => member.AllocationPercentage).Should().ContainInOrder(80, 20);
        result.Value.First().Role.Should().Be("Tech Lead");
        result.Value.First().DepartmentName.Should().Be("Engineering");
    }

    [Fact]
    public async Task A_project_with_no_team_returns_an_empty_list()
    {
        var project = await _harness.GivenProjectAsync();

        var result = await Handler.Handle(new GetProjectEmployeesQuery(project.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_project_is_a_not_found_rather_than_an_empty_list()
    {
        var result = await Handler.Handle(new GetProjectEmployeesQuery(999), CancellationToken.None);

        result.Error.Should().Be(ProjectErrors.NotFound(999));
    }

    public void Dispose() => _harness.Dispose();
}
