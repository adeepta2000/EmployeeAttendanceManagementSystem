using Attendance.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Identity;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");

        // A reset looks the presented token up by hash.
        builder.Property(t => t.TokenHash).HasMaxLength(RefreshToken.TokenHashMaxLength);
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // A new link, and every password change, revokes the user's outstanding links.
        builder.HasIndex(t => t.UserId);

        // Using a link updates "WHERE UsedUtc IS NULL", so when the same link is submitted twice at once,
        // only one request can set the password; the other gets a concurrency conflict.
        builder.Property(t => t.UsedUtc).IsConcurrencyToken();

        builder.Property(t => t.CreatedByIp).HasMaxLength(RefreshToken.IpAddressMaxLength);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId);
    }
}
