using EPM.Application.Features.Employees.CreateEmployee;
using EPM.Application.UnitTests.Infrastructure;
using EPM.Domain.Departments;
using EPM.Domain.Employees;
using FluentAssertions;

namespace EPM.Application.UnitTests.Employees;

/// <summary>
/// Covers four of the six cases the brief calls out by name: creating an employee, blocking a
/// duplicate email, blocking an unknown department, and blocking a future hire date.
/// </summary>
public sealed class CreateEmployeeHandlerTests : IDisposable
{
    private readonly SliceTestHarness _harness = new();

    private CreateEmployeeHandler Handler => new(_harness.Db, _harness.Clock);

    [Fact]
    public async Task Creating_an_employee_persists_them_as_active()
    {
        var department = await _harness.GivenDepartmentAsync();

        var result = await Handler.Handle(
            new CreateEmployeeCommand(
                "Ada", "Lovelace", "Ada.Lovelace@EPM.local", "+1 555 010 0100",
                "Principal Engineer", department.Id, _harness.Clock.Today.AddYears(-1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.DepartmentName.Should().Be("Engineering");
        result.Value.Email.Should().Be("ada.lovelace@epm.local");

        _harness.DetachAll();
        _harness.Context.Employees.Should().ContainSingle();
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_as_a_conflict()
    {
        var department = await _harness.GivenDepartmentAsync();
        await _harness.GivenEmployeeAsync(department.Id, "ada.lovelace@epm.local");

        var result = await Handler.Handle(
            new CreateEmployeeCommand(
                "Augusta", "Byron", "ada.lovelace@epm.local", null,
                "Engineer", department.Id, _harness.Clock.Today.AddYears(-1)),
            CancellationToken.None);

        result.Error.Should().Be(EmployeeErrors.EmailAlreadyExists);
        _harness.Context.Employees.Should().ContainSingle("the second employee must not be inserted");
    }

    [Fact]
    public async Task Duplicate_email_is_detected_regardless_of_the_casing_submitted()
    {
        // Addresses are normalised by the Email value object, so the uniqueness check cannot
        // be fooled by capitals — this is the case a naive string comparison would let past.
        var department = await _harness.GivenDepartmentAsync();
        await _harness.GivenEmployeeAsync(department.Id, "ada.lovelace@epm.local");

        var result = await Handler.Handle(
            new CreateEmployeeCommand(
                "Augusta", "Byron", "ADA.LOVELACE@EPM.LOCAL", null,
                "Engineer", department.Id, _harness.Clock.Today.AddYears(-1)),
            CancellationToken.None);

        result.Error.Should().Be(EmployeeErrors.EmailAlreadyExists);
    }

    [Fact]
    public async Task Unknown_department_is_rejected()
    {
        var result = await Handler.Handle(
            new CreateEmployeeCommand(
                "Ada", "Lovelace", "ada@epm.local", null,
                "Engineer", DepartmentId: 999, _harness.Clock.Today.AddYears(-1)),
            CancellationToken.None);

        result.Error.Should().Be(DepartmentErrors.NotFound(999));
        _harness.Context.Employees.Should().BeEmpty();
    }

    [Fact]
    public async Task Future_hire_date_is_rejected()
    {
        var department = await _harness.GivenDepartmentAsync();

        var result = await Handler.Handle(
            new CreateEmployeeCommand(
                "Ada", "Lovelace", "ada@epm.local", null,
                "Engineer", department.Id, _harness.Clock.Today.AddDays(1)),
            CancellationToken.None);

        result.Error.Should().Be(EmployeeErrors.HireDateInFuture);
        _harness.Context.Employees.Should().BeEmpty();
    }

    [Fact]
    public async Task Hire_date_of_today_is_accepted()
    {
        var department = await _harness.GivenDepartmentAsync();

        var result = await Handler.Handle(
            new CreateEmployeeCommand(
                "Ada", "Lovelace", "ada@epm.local", null,
                "Engineer", department.Id, _harness.Clock.Today),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Malformed_email_is_rejected()
    {
        var department = await _harness.GivenDepartmentAsync();

        var result = await Handler.Handle(
            new CreateEmployeeCommand(
                "Ada", "Lovelace", "not-an-email", null,
                "Engineer", department.Id, _harness.Clock.Today.AddYears(-1)),
            CancellationToken.None);

        result.Error.Should().Be(EmployeeErrors.EmailInvalid);
    }

    public void Dispose() => _harness.Dispose();
}
