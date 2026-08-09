using EPM.Application.Abstractions;
using EPM.Domain.Departments;
using EPM.Domain.Employees;
using EPM.Domain.Identity;
using EPM.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EPM.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // One IEntityTypeConfiguration per aggregate, discovered from this assembly. Keeps
        // this method from growing into the usual 400-line wall of fluent calls.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Note on soft delete: there is deliberately NO global query filter on
        // Employee.IsActive. The spec calls for deactivation rather than deletion, but
        // inactive employees still have to be listable — the employees page has an explicit
        // status filter, and the dashboard reports total vs active. A global filter would
        // hide them everywhere and force IgnoreQueryFilters() at nearly every call site,
        // which is a filter that has stopped filtering. IsActive is an ordinary column and
        // each query says what it wants.
        base.OnModelCreating(modelBuilder);
    }
}
