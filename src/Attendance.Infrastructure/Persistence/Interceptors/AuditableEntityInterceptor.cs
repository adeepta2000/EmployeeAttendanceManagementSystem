using Attendance.Application.Common.Interfaces;
using Attendance.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Attendance.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps the audit columns on every save from the current user and the server clock, and turns
/// deletes of <see cref="ISoftDeletable"/> entities into soft deletes.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentUser currentUser, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    /// <summary>The actor recorded when no user is signed in (background jobs, seeding).</summary>
    public const string SystemActor = "system";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInfo(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var actor = currentUser.UserId ?? SystemActor;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable)
            {
                // Unchanged first, so only IsDeleted (and the audit columns below) are written back.
                entry.State = EntityState.Unchanged;
                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    entry.Property(e => e.CreatedBy).CurrentValue = actor;
                    break;

                case EntityState.Modified:
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                    entry.Property(e => e.UpdatedBy).CurrentValue = actor;
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    break;
            }
        }
    }
}
