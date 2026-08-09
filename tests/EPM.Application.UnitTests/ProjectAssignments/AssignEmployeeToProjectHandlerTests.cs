using EPM.Application.Features.ProjectAssignments.AssignEmployeeToProject;
using EPM.Application.UnitTests.Infrastructure;
using EPM.Domain.Employees;
using EPM.Domain.Projects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.UnitTests.ProjectAssignments;

/// <summary>
/// The other two named cases from the brief — an inactive employee cannot be assigned, and an
/// employee cannot be assigned to the same project twice — plus the rules around them.
/// </summary>
public sealed class AssignEmployeeToProjectHandlerTests : IDisposable
{
    private readonly SliceTestHarness _harness = new();

    private AssignEmployeeToProjectHandler Handler => new(_harness.Db);

    [Fact]
    public async Task Assigning_an_active_employee_adds_them_to_the_team()
    {
        var department = await _harness.GivenDepartmentAsync();
        var employee = await _harness.GivenEmployeeAsync(department.Id);
        var project = await _harness.GivenProjectAsync();

        var result = await Handler.Handle(
            new AssignEmployeeToProjectCommand(project.Id, employee.Id, "Tech Lead", _harness.Clock.Today, 60),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EmployeeName.Should().Be("Ada Lovelace");
        result.Value.AllocationPercentage.Should().Be(60);

        _harness.DetachAll();
        var saved = await _harness.Context.ProjectAssignments.SingleAsync();
        saved.EmployeeId.Should().Be(employee.Id);
        saved.Allocation.Percentage.Should().Be(60);
    }

    [Fact]
    public async Task Inactive_employees_cannot_be_assigned()
    {
        var department = await _harness.GivenDepartmentAsync();
        var employee = await _harness.GivenEmployeeAsync(department.Id, active: false);
        var project = await _harness.GivenProjectAsync();

        var result = await Handler.Handle(
            new AssignEmployeeToProjectCommand(project.Id, employee.Id, "Tech Lead", _harness.Clock.Today, 60),
            CancellationToken.None);

        result.Error.Should().Be(ProjectAssignmentErrors.EmployeeInactive);
        _harness.Context.ProjectAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_employee_cannot_be_assigned_to_a_project_twice()
    {
        var department = await _harness.GivenDepartmentAsync();
        var employee = await _harness.GivenEmployeeAsync(department.Id);
        var project = await _harness.GivenProjectAsync();

        await Handler.Handle(
            new AssignEmployeeToProjectCommand(project.Id, employee.Id, "Tech Lead", _harness.Clock.Today, 60),
            CancellationToken.None);

        _harness.DetachAll();

        var result = await Handler.Handle(
            new AssignEmployeeToProjectCommand(project.Id, employee.Id, "Reviewer", _harness.Clock.Today, 10),
            CancellationToken.None);

        result.Error.Should().Be(ProjectAssignmentErrors.DuplicateAssignment);
        (await _harness.Context.ProjectAssignments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_same_employee_can_be_assigned_to_two_different_projects()
    {
        // The uniqueness rule is per project, not global — someone splitting time across two
        // projects is the normal case, not a violation.
        var department = await _harness.GivenDepartmentAsync();
        var employee = await _harness.GivenEmployeeAsync(department.Id);
        var first = await _harness.GivenProjectAsync("Apollo");
        var second = await _harness.GivenProjectAsync("Orion");

        await Handler.Handle(
            new AssignEmployeeToProjectCommand(first.Id, employee.Id, "Tech Lead", _harness.Clock.Today, 60),
            CancellationToken.None);

        _harness.DetachAll();

        var result = await Handler.Handle(
            new AssignEmployeeToProjectCommand(second.Id, employee.Id, "Engineer", _harness.Clock.Today, 40),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _harness.Context.ProjectAssignments.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Allocation_outside_one_to_hundred_is_rejected(int percentage)
    {
        var department = await _harness.GivenDepartmentAsync();
        var employee = await _harness.GivenEmployeeAsync(department.Id);
        var project = await _harness.GivenProjectAsync();

        var result = await Handler.Handle(
            new AssignEmployeeToProjectCommand(project.Id, employee.Id, "Tech Lead", _harness.Clock.Today, percentage),
            CancellationToken.None);

        result.Error.Should().Be(ProjectAssignmentErrors.AllocationOutOfRange);
    }

    [Fact]
    public async Task Assigned_date_outside_the_project_schedule_is_rejected()
    {
        var department = await _harness.GivenDepartmentAsync();
        var employee = await _harness.GivenEmployeeAsync(department.Id);

        var project = await _harness.GivenProjectAsync(
            start: _harness.Clock.Today.AddMonths(1),
            end: _harness.Clock.Today.AddMonths(6));

        var result = await Handler.Handle(
            new AssignEmployeeToProjectCommand(project.Id, employee.Id, "Tech Lead", _harness.Clock.Today, 50),
            CancellationToken.None);

        result.Error.Code.Should().Be("Assignment.DateOutsideProjectSchedule");
    }

    [Fact]
    public async Task Unknown_project_is_a_not_found()
    {
        var department = await _harness.GivenDepartmentAsync();
        var employee = await _harness.GivenEmployeeAsync(department.Id);

        var result = await Handler.Handle(
            new AssignEmployeeToProjectCommand(999, employee.Id, "Tech Lead", _harness.Clock.Today, 50),
            CancellationToken.None);

        result.Error.Should().Be(ProjectErrors.NotFound(999));
    }

    [Fact]
    public async Task Unknown_employee_is_a_not_found()
    {
        var project = await _harness.GivenProjectAsync();

        var result = await Handler.Handle(
            new AssignEmployeeToProjectCommand(project.Id, 999, "Tech Lead", _harness.Clock.Today, 50),
            CancellationToken.None);

        result.Error.Should().Be(EmployeeErrors.NotFound(999));
    }

    public void Dispose() => _harness.Dispose();
}
