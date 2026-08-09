using EPM.Application.Abstractions;
using EPM.Domain.Departments;
using EPM.Domain.Employees;
using EPM.Domain.Identity;
using EPM.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EPM.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fills an empty database with enough data to actually use the application — paging needs
/// more than three rows to demonstrate anything, and the dashboard needs a spread of
/// departments and statuses to look like anything but zeroes.
/// </summary>
/// <remarks>
/// Idempotent, one section at a time: each step checks whether its table is already populated
/// and skips if so. That means it is safe to run on every startup, and safe to re-run after
/// adding a new section without duplicating the old ones.
///
/// Everything goes through the domain factories rather than raw SQL or HasData. Slower, but
/// the seed data is then guaranteed to satisfy the same invariants as anything a user could
/// create — no fixture can quietly introduce a state the domain would have rejected.
/// </remarks>
public sealed class DatabaseSeeder(
    AppDbContext context,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock,
    IOptions<SeedOptions> options,
    ILogger<DatabaseSeeder> logger)
{
    private readonly SeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Seeding is disabled; skipping");
            return;
        }

        var departments = await SeedDepartmentsAsync(cancellationToken);
        var employees = await SeedEmployeesAsync(departments, cancellationToken);
        await SeedProjectsAsync(employees, cancellationToken);
        await SeedUsersAsync(employees, cancellationToken);
    }

    private async Task<IReadOnlyList<Department>> SeedDepartmentsAsync(CancellationToken cancellationToken)
    {
        if (await context.Departments.AnyAsync(cancellationToken))
        {
            return await context.Departments.ToListAsync(cancellationToken);
        }

        var departments = new[]
        {
            Department.Create("Engineering", "Builds and runs the product.").Value,
            Department.Create("Product", "Owns roadmap and discovery.").Value,
            Department.Create("Design", "Research, interaction and visual design.").Value,
            Department.Create("Finance", "Budgeting, payroll and reporting.").Value,
            Department.Create("People Operations", "Hiring, onboarding and employee support.").Value,
        };

        context.Departments.AddRange(departments);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} departments", departments.Length);

        return departments;
    }

    private async Task<IReadOnlyList<Employee>> SeedEmployeesAsync(
        IReadOnlyList<Department> departments,
        CancellationToken cancellationToken)
    {
        if (await context.Employees.AnyAsync(cancellationToken))
        {
            return await context.Employees.ToListAsync(cancellationToken);
        }

        var today = clock.Today;
        var employees = new List<Employee>();

        foreach (var (firstName, lastName, jobTitle, departmentIndex, yearsOfService, active) in EmployeeSeedRows)
        {
            var created = Employee.Create(
                firstName,
                lastName,
                $"{firstName}.{lastName}@epm.local".ToLowerInvariant(),
                $"+1 555 0{100 + employees.Count:000}",
                jobTitle,
                departments[departmentIndex].Id,
                today.AddDays(-(int)(yearsOfService * 365)),
                today);

            // A seed row that fails validation is a bug in this file, not user input — fail
            // loudly rather than silently producing a half-populated database.
            if (created.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed employee '{firstName} {lastName}' is invalid: {created.Error.Code}");
            }

            if (!active)
            {
                created.Value.Deactivate(clock.UtcNow);
            }

            employees.Add(created.Value);
        }

        context.Employees.AddRange(employees);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} employees", employees.Count);

        return employees;
    }

    private async Task SeedProjectsAsync(IReadOnlyList<Employee> employees, CancellationToken cancellationToken)
    {
        if (await context.Projects.AnyAsync(cancellationToken))
        {
            return;
        }

        var today = clock.Today;
        var activeEmployees = employees.Where(employee => employee.IsActive).ToList();

        var projects = new List<Project>
        {
            Project.Create("Atlas Platform Migration", "Move the core platform onto the new runtime.",
                today.AddMonths(-4), today.AddMonths(6), ProjectStatus.Active).Value,
            Project.Create("Orion Mobile App", "Native mobile client for field staff.",
                today.AddMonths(-2), today.AddMonths(8), ProjectStatus.Active).Value,
            Project.Create("Helios Reporting", "Self-service reporting and dashboards.",
                today.AddMonths(1), today.AddMonths(9), ProjectStatus.Planning).Value,
            Project.Create("Vega Billing Rewrite", "Replace the legacy billing engine.",
                today.AddMonths(-14), today.AddMonths(-2), ProjectStatus.Completed).Value,
            Project.Create("Nimbus Data Lake", "Central analytics store. Shelved after review.",
                today.AddMonths(-9), today.AddMonths(-6), ProjectStatus.Cancelled).Value,
        };

        // Spread the team across projects deterministically — a random assignment would make
        // the dashboard numbers differ between runs and turn any screenshot into a lie.
        var roles = new[] { "Tech Lead", "Engineer", "Analyst", "Designer", "QA" };
        var allocations = new[] { 100, 50, 25, 75, 40 };

        for (var projectIndex = 0; projectIndex < projects.Count; projectIndex++)
        {
            var project = projects[projectIndex];

            for (var offset = 0; offset < 4 && offset < activeEmployees.Count; offset++)
            {
                var employee = activeEmployees[(projectIndex * 3 + offset) % activeEmployees.Count];

                // Assignments must sit inside the project's schedule, so anchor them to the
                // start date rather than to today — the completed and cancelled projects
                // ended months ago.
                var assignedDate = project.Schedule.Start.AddDays(offset * 7);

                project.AssignEmployee(
                    employee.Id,
                    employee.IsActive,
                    roles[(projectIndex + offset) % roles.Length],
                    assignedDate,
                    allocations[(projectIndex + offset) % allocations.Length]);
            }
        }

        context.Projects.AddRange(projects);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} projects", projects.Count);
    }

    private async Task SeedUsersAsync(IReadOnlyList<Employee> employees, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        if (!_options.HasCredentials)
        {
            logger.LogWarning(
                "Seed:AdminPassword / ManagerPassword / UserPassword are not configured, so no login " +
                "accounts were created. Set them (see .env.example) and restart to sign in.");

            return;
        }

        // Each demo account is linked to a real employee row so "my assignments" has something
        // to show and the role behaviour is visible end to end.
        var accounts = new[]
        {
            ("admin@epm.local", "Alex Admin", _options.AdminPassword!, UserRole.Admin, 0),
            ("manager@epm.local", "Morgan Manager", _options.ManagerPassword!, UserRole.Manager, 1),
            ("user@epm.local", "Uma User", _options.UserPassword!, UserRole.User, 2),
        };

        foreach (var (email, displayName, password, role, employeeIndex) in accounts)
        {
            var user = AppUser.CreateLocal(
                email,
                displayName,
                passwordHasher.Hash(password),
                role,
                employees.Count > employeeIndex ? employees[employeeIndex].Id : null,
                clock.UtcNow);

            if (user.IsFailure)
            {
                throw new InvalidOperationException($"Seed account '{email}' is invalid: {user.Error.Code}");
            }

            context.Users.Add(user.Value);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} login accounts", accounts.Length);
    }

    // Fixed roster: same people, same departments, same tenure on every fresh database.
    // Two are inactive so the status filter and the "total vs active" dashboard tiles have
    // something to distinguish.
    private static readonly (string First, string Last, string JobTitle, int Department, double Years, bool Active)[]
        EmployeeSeedRows =
        [
            ("Ada", "Lovelace", "Principal Engineer", 0, 6.0, true),
            ("Alan", "Turing", "Staff Engineer", 0, 5.5, true),
            ("Grace", "Hopper", "Engineering Manager", 0, 8.0, true),
            ("Linus", "Torvalds", "Senior Engineer", 0, 4.0, true),
            ("Barbara", "Liskov", "Software Engineer", 0, 2.0, true),
            ("Katherine", "Johnson", "Data Engineer", 0, 3.5, true),
            ("Margaret", "Hamilton", "Head of Product", 1, 7.0, true),
            ("Don", "Norman", "Product Manager", 1, 3.0, true),
            ("Marty", "Cagan", "Product Manager", 1, 1.5, true),
            ("Jony", "Ive", "Design Director", 2, 6.5, true),
            ("Susan", "Kare", "Senior Product Designer", 2, 4.5, true),
            ("Dieter", "Rams", "UX Researcher", 2, 2.5, true),
            ("Warren", "Buffett", "Finance Director", 3, 9.0, true),
            ("Mary", "Barra", "Financial Analyst", 3, 3.0, true),
            ("Sheryl", "Sandberg", "People Operations Lead", 4, 5.0, true),
            ("Patty", "McCord", "Talent Partner", 4, 2.0, true),
            ("Nikola", "Tesla", "Research Engineer", 0, 10.0, false),
            ("Rosalind", "Franklin", "Data Analyst", 3, 4.0, false),
        ];
}
