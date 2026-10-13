using Attendance.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Attendance.Infrastructure.Persistence.Configurations.Identity;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        // Every refresh looks the token up by hash.
        builder.Property(t => t.TokenHash).HasMaxLength(RefreshToken.TokenHashMaxLength);
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Reuse detection revokes a whole family at once.
        builder.HasIndex(t => t.FamilyId);

        // A rotation updates "WHERE RevokedUtc IS NULL", so when two requests present the same token at
        // once, only one can rotate it. The other gets a concurrency conflict and treats it as reuse.
        builder.Property(t => t.RevokedUtc).IsConcurrencyToken();

        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(RefreshToken.TokenHashMaxLength);
        builder.Property(t => t.CreatedByIp).HasMaxLength(RefreshToken.IpAddressMaxLength);
        builder.Property(t => t.UserAgent).HasMaxLength(RefreshToken.UserAgentMaxLength);

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId);
    }
}
