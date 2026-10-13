using Attendance.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Identity;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength);
        builder.HasIndex(u => u.Email).IsUnique();

        // Parallel wrong-password attempts would otherwise overwrite each other's increment and get
        // past the lockout uncounted. The loser of the race retries from the database.
        builder.Property(u => u.FailedLoginCount).IsConcurrencyToken();

        // One-to-zero-or-one: EF adds a unique index on EmployeeId filtered to non-null values.
        builder.HasOne(u => u.Employee)
            .WithOne()
            .HasForeignKey<User>(u => u.EmployeeId);
    }
}
