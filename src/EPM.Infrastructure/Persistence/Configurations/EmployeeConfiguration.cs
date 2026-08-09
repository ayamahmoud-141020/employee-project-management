using EPM.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPM.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.FirstName)
            .IsRequired()
            .HasMaxLength(Employee.MaxNameLength);

        builder.Property(employee => employee.LastName)
            .IsRequired()
            .HasMaxLength(Employee.MaxNameLength);

        builder.Property(employee => employee.JobTitle)
            .IsRequired()
            .HasMaxLength(Employee.MaxJobTitleLength);

        // Email maps as an owned type rather than through a value converter, and the choice is
        // forced by querying. A converter turns the whole value object into one opaque column:
        // `employee.Email == someEmail` translates, but `employee.Email.Value.Contains(term)`
        // does not, which kills search and sorting on the column. Owned mapping exposes Value
        // as a real property EF can translate member access on. It still lands in the same
        // table as a single "Email" column — the only cost is that whole-object equality has
        // to be written as `.Value == theString`.
        builder.OwnsOne(employee => employee.Email, email =>
        {
            email.Property(value => value.Value)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(Email.MaxLength);

            // Backs the "email must be unique" rule. The create/update handlers pre-check to
            // give a readable message; this is the constraint that cannot be raced.
            email.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("UX_Employees_Email");
        });

        builder.Navigation(employee => employee.Email).IsRequired();

        // Phone stays value-converted: it is optional, and an owned type that is null for most
        // rows is more trouble than it is worth. The consequence is that `Phone.Value` can only
        // appear in a final projection, which EF evaluates client-side — never in a Where or
        // OrderBy, which must translate to SQL. Nothing searches or sorts on phone, so that
        // restriction costs nothing here.
        builder.Property(employee => employee.Phone)
            .HasConversion(
                phone => phone!.Value,
                value => PhoneNumber.CreateOptional(value).Value)
            .HasMaxLength(PhoneNumber.MaxLength);

        builder.Property(employee => employee.HireDate)
            .IsRequired();

        builder.Property(employee => employee.IsActive)
            .IsRequired();

        // The employees list is filtered by department and status far more often than
        // anything else, and the dashboard groups by exactly this pair.
        builder.HasIndex(employee => new { employee.DepartmentId, employee.IsActive })
            .HasDatabaseName("IX_Employees_Department_IsActive");

        // Default sort order for the list endpoint; without this every page-2 request scans.
        builder.HasIndex(employee => new { employee.LastName, employee.FirstName })
            .HasDatabaseName("IX_Employees_Name");

        builder.HasOne(employee => employee.Department)
            .WithMany()
            .HasForeignKey(employee => employee.DepartmentId)
            // Restrict, not Cascade: deleting a department must never quietly delete people.
            // The delete handler refuses while active employees remain, and this makes sure
            // nothing gets round it.
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(employee => employee.DomainEvents);
        builder.Ignore(employee => employee.FullName);
    }
}
