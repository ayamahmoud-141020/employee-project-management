using EPM.Domain.Departments;
using EPM.Domain.Employees;
using EPM.Domain.Identity;
using EPM.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Abstractions;

/// <summary>
/// The slice-facing view of the database.
/// </summary>
/// <remarks>
/// Deliberately not a repository-per-entity layer. DbSet already is a repository and
/// DbContext already is a unit of work; wrapping them again buys nothing but indirection and
/// tends to leak IQueryable back out anyway. What this interface does buy is a seam:
/// handlers depend on it rather than on the concrete context, so tests can hand them a
/// SQLite-backed one without dragging in the SQL Server provider.
/// </remarks>
public interface IAppDbContext
{
    DbSet<Employee> Employees { get; }

    DbSet<Department> Departments { get; }

    DbSet<Project> Projects { get; }

    DbSet<ProjectAssignment> ProjectAssignments { get; }

    DbSet<AppUser> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
