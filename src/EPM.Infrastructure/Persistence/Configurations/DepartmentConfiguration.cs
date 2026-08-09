using EPM.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPM.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");

        builder.HasKey(department => department.Id);

        builder.Property(department => department.Name)
            .IsRequired()
            .HasMaxLength(Department.MaxNameLength);

        builder.Property(department => department.Description)
            .HasMaxLength(Department.MaxDescriptionLength);

        // The handler checks for a duplicate name first to produce a friendly message, but
        // that check and the insert are not atomic. This index is what actually guarantees
        // uniqueness when two admins submit the same name at the same moment.
        builder.HasIndex(department => department.Name)
            .IsUnique()
            .HasDatabaseName("UX_Departments_Name");

        builder.Ignore(department => department.DomainEvents);
    }
}
