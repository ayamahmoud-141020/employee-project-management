using EPM.Application.Abstractions;
using EPM.Domain.Departments;
using EPM.Domain.Employees;
using EPM.Domain.Projects;
using EPM.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.UnitTests.Infrastructure;

/// <summary>
/// A throwaway database plus the ambient services handlers depend on.
/// </summary>
/// <remarks>
/// SQLite in-memory, not EF's InMemory provider. The difference matters here: InMemory is a
/// dictionary that ignores unique indexes, foreign keys and check constraints, so a test for
/// "duplicate email is rejected" would pass against it whether or not the constraint exists.
/// SQLite runs the real model and actually enforces them.
///
/// The connection is held open deliberately — an in-memory SQLite database is destroyed the
/// moment its last connection closes, so letting EF open and close per query would wipe it
/// between calls.
/// </remarks>
internal sealed class SliceTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public SliceTestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public AppDbContext Context { get; }

    public IAppDbContext Db => Context;

    public FixedClock Clock { get; } = new();

    /// <summary>
    /// Adds a department and saves, returning it with a real id. Most slice tests need one
    /// before they can create an employee.
    /// </summary>
    public async Task<Department> GivenDepartmentAsync(string name = "Engineering")
    {
        var department = Department.Create(name, null).Value;
        Context.Departments.Add(department);
        await Context.SaveChangesAsync();

        return department;
    }

    public async Task<Employee> GivenEmployeeAsync(
        int departmentId,
        string email = "ada.lovelace@epm.local",
        bool active = true)
    {
        var employee = Employee.Create(
            "Ada", "Lovelace", email, null, "Engineer",
            departmentId, Clock.Today.AddYears(-2), Clock.Today).Value;

        if (!active)
        {
            employee.Deactivate(Clock.UtcNow);
            // Cleared so the deactivation event does not fire during an unrelated test's
            // SaveChanges — the interceptor is not wired up here, but the events would
            // otherwise pile up on the tracked entity.
            employee.ClearDomainEvents();
        }

        Context.Employees.Add(employee);
        await Context.SaveChangesAsync();

        return employee;
    }

    public async Task<Project> GivenProjectAsync(
        string name = "Apollo",
        DateOnly? start = null,
        DateOnly? end = null)
    {
        var project = Project.Create(
            name,
            null,
            start ?? Clock.Today.AddMonths(-1),
            end ?? Clock.Today.AddMonths(6),
            ProjectStatus.Active).Value;

        Context.Projects.Add(project);
        await Context.SaveChangesAsync();

        return project;
    }

    /// <summary>
    /// Drops everything EF is tracking, so the next read comes from the database rather than
    /// the change tracker. Without this a test can "pass" against an in-memory object graph
    /// that was never actually persisted.
    /// </summary>
    public void DetachAll() => Context.ChangeTracker.Clear();

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

/// <summary>A clock frozen at a known instant, so date rules are deterministic.</summary>
internal sealed class FixedClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow);
}

/// <summary>A signed-in user with whatever identity a test needs.</summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => UserId.HasValue;

    public int? UserId { get; set; }

    public string? Email { get; set; }

    public Domain.Identity.UserRole? Role { get; set; }

    public int? EmployeeId { get; set; }
}
