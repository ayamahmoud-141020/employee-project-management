using EPM.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPM.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Name)
            .IsRequired()
            .HasMaxLength(Project.MaxNameLength);

        builder.Property(project => project.Description)
            .HasMaxLength(Project.MaxDescriptionLength);

        // Schedule is a two-field value object, so it maps as an owned type — two real columns
        // on the Projects table, not a separate table or a serialised blob. Queries can then
        // filter on StartDate directly.
        builder.OwnsOne(project => project.Schedule, schedule =>
        {
            schedule.Property(range => range.Start)
                .HasColumnName("StartDate")
                .IsRequired();

            // Nullable: an open-ended project has no end date. See DateRange for why a
            // placeholder date was not used instead.
            schedule.Property(range => range.End)
                .HasColumnName("EndDate");

            schedule.HasIndex(range => range.Start)
                .HasDatabaseName("IX_Projects_StartDate");
        });

        // Stored as int rather than a string. The enum values are pinned in ProjectStatus for
        // exactly this reason.
        builder.Property(project => project.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(project => project.Status)
            .HasDatabaseName("IX_Projects_Status");

        builder.HasIndex(project => project.Name)
            .IsUnique()
            .HasDatabaseName("UX_Projects_Name");

        // The collection is private on the aggregate — EF is pointed at the backing field so
        // it can populate it without the class exposing a settable list.
        builder.Metadata
            .FindNavigation(nameof(Project.Assignments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(project => project.DomainEvents);
    }
}
