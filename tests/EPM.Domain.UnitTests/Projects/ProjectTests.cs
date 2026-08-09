using EPM.Domain.Projects;
using FluentAssertions;

namespace EPM.Domain.UnitTests.Projects;

public class ProjectTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    [Fact]
    public void Create_WithValidDetails_Succeeds()
    {
        var result = Project.Create("  Apollo  ", "Billing migration.", Start, End, ProjectStatus.Planning);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Apollo");
        result.Value.Status.Should().Be(ProjectStatus.Planning);
        result.Value.Assignments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Fails(string? name)
    {
        Project.Create(name, null, Start, End, ProjectStatus.Planning)
            .Error.Should().Be(ProjectErrors.NameRequired);
    }

    [Fact]
    public void Create_WithEndDateBeforeStartDate_Fails()
    {
        Project.Create("Apollo", null, End, Start, ProjectStatus.Planning)
            .Error.Should().Be(ProjectErrors.EndDateBeforeStartDate);
    }

    [Fact]
    public void Create_WithEndDateEqualToStartDate_Succeeds()
    {
        // The spec says "greater than or equal to", so a single-day project is legal.
        Project.Create("Apollo", null, Start, Start, ProjectStatus.Planning)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutEndDate_Succeeds()
    {
        var result = Project.Create("Apollo", null, Start, null, ProjectStatus.Active);

        result.IsSuccess.Should().BeTrue();
        result.Value.Schedule.End.Should().BeNull();
    }

    [Fact]
    public void Create_WithUndefinedStatus_Fails()
    {
        Project.Create("Apollo", null, Start, End, (ProjectStatus)99)
            .Error.Should().Be(ProjectErrors.StatusInvalid);
    }

    [Fact]
    public void Create_WithDescriptionOverMaxLength_Fails()
    {
        var tooLong = new string('x', Project.MaxDescriptionLength + 1);

        Project.Create("Apollo", tooLong, Start, End, ProjectStatus.Planning)
            .Error.Should().Be(ProjectErrors.DescriptionTooLong);
    }

    [Fact]
    public void Update_ShrinkingScheduleUnderAnExistingAssignment_Fails()
    {
        var project = TestData.AProject(Start, End);
        project.AssignEmployee(1, true, "Tech Lead", new DateOnly(2026, 11, 1), 50);

        // Pulling the end date back to June would strand the November assignment outside the
        // schedule — exactly the state AssignEmployee refuses to create in the first place.
        var result = project.Update("Apollo", null, Start, new DateOnly(2026, 6, 30), ProjectStatus.Active);

        result.Error.Code.Should().Be("Project.ScheduleConflictsWithAssignments");
        project.Schedule.End.Should().Be(End, "a rejected update changes nothing");
    }

    [Fact]
    public void Update_WideningSchedule_Succeeds()
    {
        var project = TestData.AProject(Start, End);
        project.AssignEmployee(1, true, "Tech Lead", new DateOnly(2026, 11, 1), 50);

        var result = project.Update("Apollo", null, Start, new DateOnly(2027, 6, 30), ProjectStatus.Active);

        result.IsSuccess.Should().BeTrue();
        project.Schedule.End.Should().Be(new DateOnly(2027, 6, 30));
    }

    [Fact]
    public void Update_ChangesStatus()
    {
        var project = TestData.AProject(Start, End);

        project.Update("Apollo", "Done.", Start, End, ProjectStatus.Completed).IsSuccess.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Completed);
    }
}
