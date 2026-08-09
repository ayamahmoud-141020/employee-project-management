using EPM.Domain.Projects;
using FluentAssertions;

namespace EPM.Domain.UnitTests.Projects;

public class DateRangeTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    [Fact]
    public void Create_WithEndBeforeStart_Fails()
    {
        DateRange.Create(End, Start).Error.Should().Be(ProjectErrors.EndDateBeforeStartDate);
    }

    [Theory]
    [InlineData("2026-01-01", true)]  // start is inclusive
    [InlineData("2026-06-15", true)]
    [InlineData("2026-12-31", true)]  // end is inclusive
    [InlineData("2025-12-31", false)]
    [InlineData("2027-01-01", false)]
    public void Contains_TreatsBothEndpointsAsInside(string date, bool expected)
    {
        var range = DateRange.Create(Start, End).Value;

        range.Contains(DateOnly.Parse(date)).Should().Be(expected);
    }

    [Fact]
    public void Contains_OnOpenEndedRange_HasNoUpperBound()
    {
        var range = DateRange.Create(Start, null).Value;

        range.Contains(new DateOnly(2099, 1, 1)).Should().BeTrue();
        range.Contains(Start.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void Equality_IsByValue()
    {
        DateRange.Create(Start, End).Value.Should().Be(DateRange.Create(Start, End).Value);
        DateRange.Create(Start, End).Value.Should().NotBe(DateRange.Create(Start, null).Value);
    }
}

public class AllocationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Create_WithinRange_Succeeds(int percentage)
    {
        Allocation.Create(percentage).Value.Percentage.Should().Be(percentage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void Create_OutsideRange_Fails(int percentage)
    {
        Allocation.Create(percentage).Error.Should().Be(ProjectAssignmentErrors.AllocationOutOfRange);
    }
}
