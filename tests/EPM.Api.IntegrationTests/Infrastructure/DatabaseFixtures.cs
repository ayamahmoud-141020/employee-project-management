using EPM.Application.Abstractions;
using EPM.Domain.Departments;
using EPM.Domain.Employees;
using EPM.Domain.Identity;
using EPM.Domain.Projects;
using EPM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EPM.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Puts the accounts and reference data the API tests need into the shared database.
/// </summary>
/// <remarks>
/// Runs once for the assembly and is idempotent, because a shared container means several
/// test classes will call it. Test-created rows use unique names so classes cannot collide;
/// nothing here is torn down between tests.
/// </remarks>
public static class DatabaseFixtures
{
    public const string AdminEmail = "admin@epm.local";
    public const string ManagerEmail = "manager@epm.local";
    public const string UserEmail = "user@epm.local";
    public const string Password = "Integration#Test123";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _seeded;

    public static async Task EnsureSeededAsync(ApiFactory factory)
    {
        await Gate.WaitAsync();

        try
        {
            if (_seeded)
            {
                return;
            }

            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            if (await context.Users.AnyAsync())
            {
                _seeded = true;
                return;
            }

            var engineering = Department.Create("Engineering", "Builds the product.").Value;
            var finance = Department.Create("Finance", null).Value;
            context.Departments.AddRange(engineering, finance);
            await context.SaveChangesAsync();

            var ada = Employee.Create(
                "Ada", "Lovelace", "ada.lovelace@epm.local", "+1 555 010 0100",
                "Principal Engineer", engineering.Id, clock.Today.AddYears(-4), clock.Today).Value;

            var grace = Employee.Create(
                "Grace", "Hopper", "grace.hopper@epm.local", null,
                "Engineering Manager", engineering.Id, clock.Today.AddYears(-6), clock.Today).Value;

            var tesla = Employee.Create(
                "Nikola", "Tesla", "nikola.tesla@epm.local", null,
                "Research Engineer", engineering.Id, clock.Today.AddYears(-9), clock.Today).Value;

            tesla.Deactivate(clock.UtcNow);
            tesla.ClearDomainEvents();

            context.Employees.AddRange(ada, grace, tesla);
            await context.SaveChangesAsync();

            var project = Project.Create(
                "Atlas Migration", "Move the platform.",
                clock.Today.AddMonths(-2), clock.Today.AddMonths(8), ProjectStatus.Active).Value;

            context.Projects.Add(project);
            await context.SaveChangesAsync();

            foreach (var (email, name, role, employeeId) in new[]
                     {
                         (AdminEmail, "Alex Admin", UserRole.Admin, (int?)ada.Id),
                         (ManagerEmail, "Morgan Manager", UserRole.Manager, grace.Id),
                         (UserEmail, "Uma User", UserRole.User, null),
                     })
            {
                context.Users.Add(
                    AppUser.CreateLocal(email, name, hasher.Hash(Password), role, employeeId, clock.UtcNow).Value);
            }

            await context.SaveChangesAsync();

            _seeded = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<int> GetDepartmentIdAsync(ApiFactory factory, string name = "Engineering")
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Departments.Where(d => d.Name == name).Select(d => d.Id).FirstAsync();
    }

    public static async Task<int> GetEmployeeIdAsync(ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Employees.Where(e => e.Email.Value == email).Select(e => e.Id).FirstAsync();
    }

    public static async Task<int> GetProjectIdAsync(ApiFactory factory, string name = "Atlas Migration")
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.Projects.Where(p => p.Name == name).Select(p => p.Id).FirstAsync();
    }
}
