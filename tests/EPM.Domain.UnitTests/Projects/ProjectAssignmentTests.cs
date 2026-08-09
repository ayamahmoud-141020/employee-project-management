using EPM.Domain.Projects;
using FluentAssertions;

namespace EPM.Domain.UnitTests.Projects;

/// <summary>
/// The assignment rules from spec section 4. They are exercised through <see cref="Project"/>
/// because that is the only way application code can reach them — ProjectAssignment's factory
/// is internal by design.
/// </summary>
public class ProjectAssignmentTests
{
    private const int EmployeeId = 42;
    private static readonly DateOnly InsideSchedule = new(2026, 3, 1);

    [Fact]
    public void AssignEmployee_WithValidInput_AddsToTeam()
    {
        var project = TestData.AProject();

        var result = project.AssignEmployee(EmployeeId, employeeIsActive: true, "Tech Lead", InsideSchedule, 50);

        result.IsSuccess.Should().BeTrue();
        project.Assignments.Should().ContainSingle();
        project.Assignments.Single().Allocation.Percentage.Should().Be(50);
    }

    [Fact]
    public void AssignEmployee_WhenEmployeeIsInactive_Fails()
    {
        var project = TestData.AProject();

        var result = project.AssignEmployee(EmployeeId, employeeIsActive: false, "Tech Lead", InsideSchedule, 50);

        result.Error.Should().Be(ProjectAssignmentErrors.EmployeeInactive);
        project.Assignments.Should().BeEmpty();
    }

    [Fact]
    public void AssignEmployee_TwiceForSameEmployee_Fails()
    {
        var project = TestData.AProject();
        project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, 50);

        var result = project.AssignEmployee(EmployeeId, true, "Reviewer", InsideSchedule, 10);

        result.Error.Should().Be(ProjectAssignmentErrors.DuplicateAssignment);
        project.Assignments.Should().ContainSingle("the second attempt must not add a row");
    }

    [Fact]
    public void AssignEmployee_ForADifferentEmployee_Succeeds()
    {
        var project = TestData.AProject();
        project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, 50);

        var result = project.AssignEmployee(EmployeeId + 1, true, "Analyst", InsideSchedule, 25);

        result.IsSuccess.Should().BeTrue();
        project.Assignments.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void AssignEmployee_WithAllocationOutsideOneToHundred_Fails(int percentage)
    {
        var project = TestData.AProject();

        var result = project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, percentage);

        result.Error.Should().Be(ProjectAssignmentErrors.AllocationOutOfRange);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void AssignEmployee_AtAllocationBoundaries_Succeeds(int percentage)
    {
        var project = TestData.AProject();

        project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, percentage)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssignEmployee_BeforeProjectStart_Fails()
    {
        var project = TestData.AProject(start: new DateOnly(2026, 2, 1), end: new DateOnly(2026, 12, 31));

        var result = project.AssignEmployee(EmployeeId, true, "Tech Lead", new DateOnly(2026, 1, 31), 50);

        result.Error.Code.Should().Be("Assignment.DateOutsideProjectSchedule");
    }

    [Fact]
    public void AssignEmployee_AfterProjectEnd_Fails()
    {
        var project = TestData.AProject(start: new DateOnly(2026, 2, 1), end: new DateOnly(2026, 3, 31));

        var result = project.AssignEmployee(EmployeeId, true, "Tech Lead", new DateOnly(2026, 4, 1), 50);

        result.Error.Code.Should().Be("Assignment.DateOutsideProjectSchedule");
    }

    [Fact]
    public void AssignEmployee_OnOpenEndedProject_AcceptsAnyDateFromStart()
    {
        var project = TestData.AnOpenEndedProject(start: new DateOnly(2026, 2, 1));

        project.AssignEmployee(EmployeeId, true, "Tech Lead", new DateOnly(2030, 1, 1), 50)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssignEmployee_WithoutRole_Fails()
    {
        var project = TestData.AProject();

        var result = project.AssignEmployee(EmployeeId, true, "  ", InsideSchedule, 50);

        result.Error.Should().Be(ProjectAssignmentErrors.RoleRequired);
    }

    [Fact]
    public void AssignEmployee_ChecksInactiveBeforeDuplicate()
    {
        // Both rules are broken here. The order matters for the message the user sees, so it
        // is pinned rather than left to whichever check happens to run first after a refactor.
        var project = TestData.AProject();
        project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, 50);

        var result = project.AssignEmployee(EmployeeId, employeeIsActive: false, "Tech Lead", InsideSchedule, 50);

        result.Error.Should().Be(ProjectAssignmentErrors.EmployeeInactive);
    }

    [Fact]
    public void RemoveEmployee_WhenAssigned_Succeeds()
    {
        var project = TestData.AProject();
        project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, 50);

        project.RemoveEmployee(EmployeeId).IsSuccess.Should().BeTrue();
        project.Assignments.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEmployee_WhenNotAssigned_Fails()
    {
        var project = TestData.AProject();

        project.RemoveEmployee(EmployeeId).Error.Code.Should().Be("Assignment.NotFound");
    }

    [Fact]
    public void RemoveEmployeeIfAssigned_ReportsWhetherAnythingChanged()
    {
        var project = TestData.AProject();
        project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, 50);

        project.RemoveEmployeeIfAssigned(EmployeeId).Should().BeTrue();
        project.RemoveEmployeeIfAssigned(EmployeeId).Should().BeFalse();
    }

    [Fact]
    public void UpdateAssignment_ChangesRoleAndAllocation()
    {
        var project = TestData.AProject();
        project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, 50);

        var result = project.UpdateAssignment(EmployeeId, "Architect", 80);

        result.IsSuccess.Should().BeTrue();
        project.Assignments.Single().Role.Should().Be("Architect");
        project.Assignments.Single().Allocation.Percentage.Should().Be(80);
    }

    [Fact]
    public void UpdateAssignment_WithInvalidAllocation_LeavesAssignmentUnchanged()
    {
        var project = TestData.AProject();
        project.AssignEmployee(EmployeeId, true, "Tech Lead", InsideSchedule, 50);

        project.UpdateAssignment(EmployeeId, "Architect", 150)
            .Error.Should().Be(ProjectAssignmentErrors.AllocationOutOfRange);

        project.Assignments.Single().Role.Should().Be("Tech Lead");
        project.Assignments.Single().Allocation.Percentage.Should().Be(50);
    }
}
