using System.Net;
using System.Net.Http.Json;
using EPM.Api.IntegrationTests.Infrastructure;
using EPM.Application.Common;
using EPM.Application.Features.Employees.Contracts;
using FluentAssertions;

namespace EPM.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class EmployeesApiTests(ApiFactory factory) : IAsyncLifetime
{
    private HttpClient _admin = null!;
    private int _departmentId;

    public async Task InitializeAsync()
    {
        await DatabaseFixtures.EnsureSeededAsync(factory);
        _admin = await factory.CreateClientAsAsync(DatabaseFixtures.AdminEmail, DatabaseFixtures.Password);
        _departmentId = await DatabaseFixtures.GetDepartmentIdAsync(factory);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_read_update_and_deactivate_round_trip()
    {
        var email = $"round.trip.{Guid.NewGuid():N}@epm.local";

        var created = await _admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Round",
            lastName = "Trip",
            email,
            phone = "+1 555 010 0199",
            jobTitle = "Engineer",
            departmentId = _departmentId,
            hireDate = "2024-01-15",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull("a 201 should say where the new resource lives");

        var createdBody = await ReadAsync<EmployeeResponse>(created);
        var id = createdBody.Data!.Id;
        createdBody.Data.IsActive.Should().BeTrue();
        createdBody.Data.DepartmentName.Should().Be("Engineering");

        var updated = await _admin.PutAsJsonAsync($"/api/employees/{id}", new
        {
            firstName = "Round",
            lastName = "Tripper",
            email,
            phone = (string?)null,
            jobTitle = "Senior Engineer",
            departmentId = _departmentId,
            hireDate = "2024-01-15",
        });

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<EmployeeResponse>(updated)).Data!.JobTitle.Should().Be("Senior Engineer");

        var deactivated = await _admin.DeleteAsync($"/api/employees/{id}");
        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);

        // The record survives the delete — that is the whole point of soft deletion.
        var afterDelete = await _admin.GetAsync($"/api/employees/{id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await afterDelete.Content.ReadFromJsonAsync<ApiEnvelope<EmployeeDetail>>(ApiFactory.Json);
        detail!.Data!.Employee.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_email_returns_409_with_a_stable_code()
    {
        var response = await _admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Duplicate",
            lastName = "Attempt",
            email = "ada.lovelace@epm.local",
            jobTitle = "Engineer",
            departmentId = _departmentId,
            hireDate = "2024-01-01",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await ReadAsync<object>(response);
        body.Success.Should().BeFalse();
        body.Message.Should().Be("Employee email already exists.");
        body.Code.Should().Be("Employee.EmailExists");
    }

    [Fact]
    public async Task Validation_failures_come_back_keyed_by_field()
    {
        // The frontend attaches each message to its own form control, so the shape of this
        // response is part of the contract, not just the status code.
        var response = await _admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "",
            lastName = "Nameless",
            email = "not-an-email",
            jobTitle = "",
            departmentId = 0,
            hireDate = "2099-01-01",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await ReadAsync<object>(response);
        body.Code.Should().Be("Validation.Failed");
        body.Errors.Should().ContainKeys("firstName", "email", "jobTitle", "departmentId", "hireDate");
        body.Errors!["hireDate"].Should().Contain("Hire date cannot be in the future.");
    }

    [Fact]
    public async Task Unknown_department_returns_404()
    {
        var response = await _admin.PostAsJsonAsync("/api/employees", new
        {
            firstName = "Orphan",
            lastName = "Employee",
            email = $"orphan.{Guid.NewGuid():N}@epm.local",
            jobTitle = "Engineer",
            departmentId = 999_999,
            hireDate = "2024-01-01",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadAsync<object>(response)).Code.Should().Be("Department.NotFound");
    }

    [Fact]
    public async Task Unknown_employee_returns_404()
    {
        var response = await _admin.GetAsync("/api/employees/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadAsync<object>(response)).Code.Should().Be("Employee.NotFound");
    }

    [Fact]
    public async Task Listing_pages_filters_and_sorts_server_side()
    {
        var page = await _admin.GetAsync("/api/employees?page=1&pageSize=2&sortBy=lastName&sortDescending=false");
        page.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadAsync<PagedResult<EmployeeResponse>>(page);
        body.Data!.Items.Should().HaveCountLessThanOrEqualTo(2);
        body.Data.TotalCount.Should().BeGreaterThan(2, "the count reflects all matches, not the page");
        body.Data.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task The_active_filter_selects_only_matching_employees()
    {
        var response = await _admin.GetAsync("/api/employees?isActive=false&pageSize=50");

        var body = await ReadAsync<PagedResult<EmployeeResponse>>(response);
        body.Data!.Items.Should().NotBeEmpty();
        body.Data.Items.Should().OnlyContain(employee => !employee.IsActive);
    }

    [Fact]
    public async Task Page_size_is_capped_server_side()
    {
        var response = await _admin.GetAsync("/api/employees?pageSize=100000");

        (await ReadAsync<PagedResult<EmployeeResponse>>(response)).Data!.PageSize
            .Should().Be(PagingOptions.MaxPageSize);
    }

    private static async Task<ApiEnvelope<T>> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(ApiFactory.Json))!;

    private sealed record EmployeeDetail(EmployeeResponse Employee);
}
