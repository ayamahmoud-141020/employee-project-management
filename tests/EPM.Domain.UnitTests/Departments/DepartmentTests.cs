using EPM.Domain.Departments;
using FluentAssertions;

namespace EPM.Domain.UnitTests.Departments;

public class DepartmentTests
{
    [Fact]
    public void Create_WithValidName_Succeeds()
    {
        var result = Department.Create("  Engineering  ", "Builds the product.");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Engineering");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Fails(string? name)
    {
        Department.Create(name, null).Error.Should().Be(DepartmentErrors.NameRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankDescription_StoresNull(string? description)
    {
        // Empty string and NULL would sort and filter differently; collapsing them at the
        // boundary means queries never have to handle both.
        Department.Create("Engineering", description).Value.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithNameOverMaxLength_Fails()
    {
        var tooLong = new string('a', Department.MaxNameLength + 1);

        Department.Create(tooLong, null).Error.Should().Be(DepartmentErrors.NameTooLong);
    }

    [Fact]
    public void Update_AppliesNameAndDescription()
    {
        var department = Department.Create("Engineering", null).Value;

        var result = department.Update("Platform Engineering", "Owns the platform.");

        result.IsSuccess.Should().BeTrue();
        department.Name.Should().Be("Platform Engineering");
        department.Description.Should().Be("Owns the platform.");
    }

    [Fact]
    public void Update_WithBlankName_LeavesDepartmentUnchanged()
    {
        var department = Department.Create("Engineering", null).Value;

        department.Update(" ", "whatever").Error.Should().Be(DepartmentErrors.NameRequired);
        department.Name.Should().Be("Engineering");
    }
}
