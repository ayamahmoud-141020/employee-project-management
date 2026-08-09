using EPM.Application.Common;
using EPM.Application.Features.Employees.GetEmployees;
using EPM.Application.UnitTests.Infrastructure;
using EPM.Domain.Employees;
using FluentAssertions;

namespace EPM.Application.UnitTests.Employees;

/// <summary>
/// Search, filtering, sorting and paging — all of it running as SQL against SQLite, so these
/// exercise the real translated queries rather than LINQ over in-memory lists.
/// </summary>
public sealed class GetEmployeesHandlerTests : IAsyncLifetime
{
    private readonly SliceTestHarness _harness = new();

    private int _engineeringId;
    private int _financeId;

    private GetEmployeesHandler Handler => new(_harness.Db);

    public async Task InitializeAsync()
    {
        var engineering = await _harness.GivenDepartmentAsync("Engineering");
        var finance = await _harness.GivenDepartmentAsync("Finance");

        _engineeringId = engineering.Id;
        _financeId = finance.Id;

        await AddAsync("Ada", "Lovelace", "ada@epm.local", "Principal Engineer", engineering.Id, 5, active: true);
        await AddAsync("Alan", "Turing", "alan@epm.local", "Staff Engineer", engineering.Id, 3, active: true);
        await AddAsync("Grace", "Hopper", "grace@epm.local", "Engineering Manager", engineering.Id, 8, active: true);
        await AddAsync("Warren", "Buffett", "warren@epm.local", "Finance Director", finance.Id, 10, active: true);
        await AddAsync("Nikola", "Tesla", "nikola@epm.local", "Research Engineer", engineering.Id, 12, active: false);

        _harness.DetachAll();
    }

    [Fact]
    public async Task Returns_every_employee_by_default_including_inactive_ones()
    {
        var result = await Query(new PagingOptions());

        result.Value.TotalCount.Should().Be(5);
        result.Value.Items.Should().Contain(employee => !employee.IsActive);
    }

    [Fact]
    public async Task Total_count_reflects_the_filtered_set_not_the_page()
    {
        // The pager needs "of 5", not "of 2" — this is the assertion that catches a count
        // query accidentally being applied after Skip/Take.
        var result = await Query(new PagingOptions { PageSize = 2 });

        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(5);
        result.Value.TotalPages.Should().Be(3);
        result.Value.HasNextPage.Should().BeTrue();
        result.Value.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task Pages_do_not_overlap_or_drop_anyone()
    {
        var first = await Query(new PagingOptions { Page = 1, PageSize = 2 });
        var second = await Query(new PagingOptions { Page = 2, PageSize = 2 });
        var third = await Query(new PagingOptions { Page = 3, PageSize = 2 });

        var everyone = first.Value.Items
            .Concat(second.Value.Items)
            .Concat(third.Value.Items)
            .Select(employee => employee.Id)
            .ToList();

        everyone.Should().OnlyHaveUniqueItems().And.HaveCount(5);
    }

    [Fact]
    public async Task A_page_beyond_the_end_is_empty_rather_than_an_error()
    {
        var result = await Query(new PagingOptions { Page = 99, PageSize = 2 });

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        var result = await Query(new PagingOptions { PageSize = 100_000 });

        result.Value.PageSize.Should().Be(PagingOptions.MaxPageSize);
    }

    [Fact]
    public async Task Search_matches_name_email_and_job_title()
    {
        (await Query(new PagingOptions { Search = "Lovelace" })).Value.TotalCount.Should().Be(1);
        (await Query(new PagingOptions { Search = "alan@epm" })).Value.TotalCount.Should().Be(1);
        (await Query(new PagingOptions { Search = "Engineer" })).Value.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task Filters_combine_rather_than_replace_each_other()
    {
        var result = await Query(new PagingOptions(), departmentId: _engineeringId, isActive: true);

        result.Value.TotalCount.Should().Be(3, "Engineering has four people but one is inactive");
        result.Value.Items.Should().OnlyContain(employee => employee.DepartmentName == "Engineering");
    }

    [Fact]
    public async Task Filtering_by_department_works()
    {
        var result = await Query(new PagingOptions(), departmentId: _financeId);

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Single().FullName.Should().Be("Warren Buffett");
    }

    [Fact]
    public async Task Sorting_ascending_and_descending_are_mirror_images()
    {
        var ascending = await Query(new PagingOptions { SortBy = "lastName" });
        var descending = await Query(new PagingOptions { SortBy = "lastName", SortDescending = true });

        ascending.Value.Items.First().LastName.Should().Be("Buffett");
        descending.Value.Items.First().LastName.Should().Be("Turing");
    }

    [Fact]
    public async Task Sorting_by_a_related_column_works()
    {
        var result = await Query(new PagingOptions { SortBy = "department" });

        result.Value.Items.First().DepartmentName.Should().Be("Engineering");
        result.Value.Items.Last().DepartmentName.Should().Be("Finance");
    }

    [Fact]
    public async Task An_unknown_sort_column_falls_back_to_the_default_rather_than_failing()
    {
        // The whitelist means a hostile or mistyped ?sortBy= degrades to the default order.
        // If this ever throws, SortMap has stopped protecting the query.
        var result = await Query(new PagingOptions { SortBy = "'; DROP TABLE Employees; --" });

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.First().LastName.Should().Be("Buffett", "the default sort is by last name");
    }

    [Fact]
    public async Task Hire_date_range_filters_are_inclusive()
    {
        var cutoff = _harness.Clock.Today.AddYears(-6);

        var result = await Query(new PagingOptions(), hiredTo: cutoff);

        result.Value.Items.Should().OnlyContain(employee => employee.HireDate <= cutoff);
        result.Value.TotalCount.Should().Be(3, "Grace, Warren and Nikola were hired more than six years ago");
    }

    private async Task<Domain.Abstractions.Result<PagedResult<Application.Features.Employees.Contracts.EmployeeResponse>>>
        Query(PagingOptions paging, int? departmentId = null, bool? isActive = null, DateOnly? hiredTo = null) =>
        await Handler.Handle(
            new GetEmployeesQuery(paging, departmentId, isActive, HiredFrom: null, HiredTo: hiredTo),
            CancellationToken.None);

    private async Task AddAsync(
        string first, string last, string email, string jobTitle, int departmentId, int yearsAgo, bool active)
    {
        var employee = Employee.Create(
            first, last, email, null, jobTitle, departmentId,
            _harness.Clock.Today.AddYears(-yearsAgo), _harness.Clock.Today).Value;

        if (!active)
        {
            employee.Deactivate(_harness.Clock.UtcNow);
            employee.ClearDomainEvents();
        }

        _harness.Context.Employees.Add(employee);
        await _harness.Context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _harness.Dispose();

        return Task.CompletedTask;
    }
}
