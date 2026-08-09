using EPM.Domain.Employees;
using FluentAssertions;

namespace EPM.Domain.UnitTests.Employees;

public class EmployeeTests
{
    [Fact]
    public void Create_WithValidDetails_ReturnsActiveEmployee()
    {
        var result = Employee.Create(
            firstName: "  Ada  ",
            lastName: "Lovelace",
            email: "Ada.Lovelace@EPM.local",
            phone: "+1 555 010 0100",
            jobTitle: "Principal Engineer",
            departmentId: TestData.DepartmentId,
            hireDate: TestData.Yesterday,
            today: TestData.Today);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.FullName.Should().Be("Ada Lovelace", "surrounding whitespace is trimmed on the way in");
        result.Value.Email.Value.Should().Be("ada.lovelace@epm.local", "addresses are normalised to lower case");
    }

    [Fact]
    public void Create_WithHireDateToday_Succeeds()
    {
        // The rule is "not in the future", so today itself has to be allowed — an off-by-one
        // here would reject everyone hired this morning.
        var result = Employee.Create(
            "Ada", "Lovelace", "ada@epm.local", null, "Engineer",
            TestData.DepartmentId, TestData.Today, TestData.Today);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithFutureHireDate_Fails()
    {
        var result = Employee.Create(
            "Ada", "Lovelace", "ada@epm.local", null, "Engineer",
            TestData.DepartmentId, TestData.Tomorrow, TestData.Today);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(EmployeeErrors.HireDateInFuture);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutFirstName_Fails(string? firstName)
    {
        var result = Employee.Create(
            firstName, "Lovelace", "ada@epm.local", null, "Engineer",
            TestData.DepartmentId, TestData.Yesterday, TestData.Today);

        result.Error.Should().Be(EmployeeErrors.FirstNameRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Create_WithoutLastName_Fails(string? lastName)
    {
        var result = Employee.Create(
            "Ada", lastName, "ada@epm.local", null, "Engineer",
            TestData.DepartmentId, TestData.Yesterday, TestData.Today);

        result.Error.Should().Be(EmployeeErrors.LastNameRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithoutDepartment_Fails(int departmentId)
    {
        var result = Employee.Create(
            "Ada", "Lovelace", "ada@epm.local", null, "Engineer",
            departmentId, TestData.Yesterday, TestData.Today);

        result.Error.Should().Be(EmployeeErrors.DepartmentRequired);
    }

    [Fact]
    public void Create_WithoutJobTitle_Fails()
    {
        var result = Employee.Create(
            "Ada", "Lovelace", "ada@epm.local", null, " ",
            TestData.DepartmentId, TestData.Yesterday, TestData.Today);

        result.Error.Should().Be(EmployeeErrors.JobTitleRequired);
    }

    [Fact]
    public void Create_WithNameOverMaxLength_Fails()
    {
        var tooLong = new string('a', Employee.MaxNameLength + 1);

        var result = Employee.Create(
            tooLong, "Lovelace", "ada@epm.local", null, "Engineer",
            TestData.DepartmentId, TestData.Yesterday, TestData.Today);

        result.Error.Should().Be(EmployeeErrors.NameTooLong);
    }

    [Fact]
    public void Create_WithoutPhone_Succeeds()
    {
        var result = Employee.Create(
            "Ada", "Lovelace", "ada@epm.local", null, "Engineer",
            TestData.DepartmentId, TestData.Yesterday, TestData.Today);

        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().BeNull("phone is optional");
    }

    [Fact]
    public void Deactivate_OnActiveEmployee_MarksInactiveAndRaisesEvent()
    {
        var employee = TestData.AnActiveEmployee();

        var result = employee.Deactivate(TestData.UtcNow);

        result.IsSuccess.Should().BeTrue();
        employee.IsActive.Should().BeFalse();
        employee.DeactivatedAtUtc.Should().Be(TestData.UtcNow);
        employee.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Deactivate_Twice_Fails()
    {
        var employee = TestData.AnActiveEmployee();
        employee.Deactivate(TestData.UtcNow);

        var result = employee.Deactivate(TestData.UtcNow);

        result.Error.Should().Be(EmployeeErrors.AlreadyInactive);
    }

    [Fact]
    public void Reactivate_OnInactiveEmployee_ClearsDeactivationTimestamp()
    {
        var employee = TestData.AnInactiveEmployee();

        var result = employee.Reactivate();

        result.IsSuccess.Should().BeTrue();
        employee.IsActive.Should().BeTrue();
        employee.DeactivatedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Update_WithFutureHireDate_LeavesEmployeeUnchanged()
    {
        var employee = TestData.AnActiveEmployee();
        var originalTitle = employee.JobTitle;

        var result = employee.Update(
            "Ada", "Lovelace", "ada@epm.local", null, "Distinguished Engineer",
            TestData.DepartmentId, TestData.Tomorrow, TestData.Today);

        result.Error.Should().Be(EmployeeErrors.HireDateInFuture);
        // Everything is validated before anything is written, so a rejected edit cannot
        // leave the aggregate half-updated.
        employee.JobTitle.Should().Be(originalTitle);
    }

    [Fact]
    public void Update_WithValidDetails_AppliesEveryField()
    {
        var employee = TestData.AnActiveEmployee();

        var result = employee.Update(
            "Augusta", "Byron", "augusta.byron@epm.local", "+44 20 7946 0100", "Head of Engineering",
            departmentId: 9, hireDate: TestData.Yesterday, today: TestData.Today);

        result.IsSuccess.Should().BeTrue();
        employee.FullName.Should().Be("Augusta Byron");
        employee.Email.Value.Should().Be("augusta.byron@epm.local");
        employee.DepartmentId.Should().Be(9);
        employee.JobTitle.Should().Be("Head of Engineering");
    }
}
