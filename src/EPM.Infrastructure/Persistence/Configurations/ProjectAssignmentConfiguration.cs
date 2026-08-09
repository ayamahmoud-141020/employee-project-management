using EPM.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPM.Infrastructure.Persistence.Configurations;

public sealed class ProjectAssignmentConfiguration : IEntityTypeConfiguration<ProjectAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectAssignment> builder)
    {
        builder.ToTable("ProjectAssignments");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Role)
            .IsRequired()
            .HasMaxLength(ProjectAssignment.MaxRoleLength);

        builder.Property(assignment => assignment.AssignedDate)
            .IsRequired();

        // Owned rather than value-converted, for the same reason as Employee.Email. A value
        // converter makes the column opaque to the query translator: EF will happily client-
        // evaluate `.Percentage` in a final projection, but ORDER BY and WHERE have to run on
        // the server, and this list is sorted by allocation. Owned mapping keeps it a single
        // AllocationPercentage column while leaving the member accessible to LINQ.
        builder.OwnsOne(assignment => assignment.Allocation, allocation =>
        {
            allocation.Property(value => value.Percentage)
                .HasColumnName("AllocationPercentage")
                .IsRequired();
        });

        builder.Navigation(assignment => assignment.Allocation).IsRequired();

        // The 1-100 rule lives in the value object; this is the database's own copy, because a
        // constraint that only exists in C# does not protect against a hand-written UPDATE
        // during a support incident.

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ProjectAssignments_AllocationPercentage",
            $"[AllocationPercentage] BETWEEN {Allocation.Minimum} AND {Allocation.Maximum}"));

        // "An employee cannot be assigned to the same project more than once." The aggregate
        // enforces this in memory; this index enforces it across concurrent requests, where
        // two transactions can each see an empty assignment list and both decide to insert.
        builder.HasIndex(assignment => new { assignment.ProjectId, assignment.EmployeeId })
            .IsUnique()
            .HasDatabaseName("UX_ProjectAssignments_Project_Employee");

        builder.HasIndex(assignment => assignment.EmployeeId)
            .HasDatabaseName("IX_ProjectAssignments_EmployeeId");

        builder.HasOne(assignment => assignment.Project)
            .WithMany(project => project.Assignments)
            .HasForeignKey(assignment => assignment.ProjectId)
            // Assignments belong to the project aggregate and have no meaning without it,
            // so deleting a project takes its assignments with it.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(assignment => assignment.Employee)
            .WithMany()
            .HasForeignKey(assignment => assignment.EmployeeId)
            // Restrict on the employee side: an employee is deactivated, never deleted, so a
            // cascade here would only ever fire by accident.
            .OnDelete(DeleteBehavior.Restrict);
    }
}
