using EPM.Application.Features.Departments.DeleteDepartment;
using EPM.Application.UnitTests.Infrastructure;
using EPM.Domain.Departments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.UnitTests.Departments;

public sealed class DeleteDepartmentHandlerTests : IDisposable
{
    private readonly SliceTestHarness _harness = new();

    private DeleteDepartmentHandler Handler => new(_harness.Db);

    [Fact]
    public async Task An_empty_department_can_be_deleted()
    {
        var department = await _harness.GivenDepartmentAsync();

        var result = await Handler.Handle(new DeleteDepartmentCommand(department.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _harness.Context.Departments.Should().BeEmpty();
    }

    [Fact]
    public async Task A_department_with_active_employees_cannot_be_deleted()
    {
        var department = await _harness.GivenDepartmentAsync();
        await _harness.GivenEmployeeAsync(department.Id);

        var result = await Handler.Handle(new DeleteDepartmentCommand(department.Id), CancellationToken.None);

        result.Error.Should().Be(DepartmentErrors.HasActiveEmployees(1));
        (await _harness.Context.Departments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_department_with_only_inactive_employees_is_still_blocked()
    {
        // Deactivated employees keep their foreign key, so deleting the department would fail
        // at the database. The handler refuses first, with a reason the user can act on.
        var department = await _harness.GivenDepartmentAsync();
        await _harness.GivenEmployeeAsync(department.Id, active: false);

        var result = await Handler.Handle(new DeleteDepartmentCommand(department.Id), CancellationToken.None);

        result.Error.Code.Should().Be("Department.HasEmployees");
    }

    [Fact]
    public async Task Deleting_an_unknown_department_is_a_not_found()
    {
        var result = await Handler.Handle(new DeleteDepartmentCommand(999), CancellationToken.None);

        result.Error.Should().Be(DepartmentErrors.NotFound(999));
    }

    public void Dispose() => _harness.Dispose();
}
