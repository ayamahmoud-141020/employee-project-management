using EPM.Domain.Employees;
using EPM.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EPM.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        // Owned rather than value-converted, for the same reason as Employee.Email — the login
        // lookup compares on Value, and member access has to be translatable.
        builder.OwnsOne(user => user.Email, email =>
        {
            email.Property(value => value.Value)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(Email.MaxLength);

            email.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("UX_Users_Email");
        });

        builder.Navigation(user => user.Email).IsRequired();

        builder.Property(user => user.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        // Null for SSO-only accounts. The column stays wide enough for the PBKDF2 format
        // written by Pbkdf2PasswordHasher (algorithm, iterations, salt and subkey, base64).
        builder.Property(user => user.PasswordHash)
            .HasMaxLength(512);

        // Entra ID object ids are GUIDs today, but the claim is specified as an opaque string,
        // so it is stored as one rather than as uniqueidentifier.
        builder.Property(user => user.ExternalObjectId)
            .HasMaxLength(100);

        builder.Property(user => user.Role)
            .HasConversion<int>()
            .IsRequired();

        // Filtered unique index: the returning-SSO-user lookup matches on this column, and
        // every local account leaves it NULL. Without the filter, the second password-only
        // account would collide with the first on NULL under SQL Server's rules.
        builder.HasIndex(user => user.ExternalObjectId)
            .IsUnique()
            .HasFilter("[ExternalObjectId] IS NOT NULL")
            .HasDatabaseName("UX_Users_ExternalObjectId");

        builder.HasOne(user => user.Employee)
            .WithMany()
            .HasForeignKey(user => user.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Metadata
            .FindNavigation(nameof(AppUser.RefreshTokens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(user => user.DomainEvents);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Token)
            .IsRequired()
            .HasMaxLength(200);

        // Every refresh request looks the token up by value, so this needs to be an index
        // seek. Unique as well — two accounts sharing a token value would be a generator bug
        // worth failing loudly on.
        builder.HasIndex(token => token.Token)
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokens_Token");

        builder.HasOne<AppUser>()
            .WithMany(user => user.RefreshTokens)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
