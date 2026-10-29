using System.Linq.Expressions;
using Attendance.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Attendance.Infrastructure.Persistence;

/// <summary>
/// Data conventions that apply to every table, so a new entity can't forget them.
/// </summary>
internal static class ModelBuilderConventions
{
    /// <summary>Name of the soft-delete query filter; pass it to <c>IgnoreQueryFilters</c> to include deleted rows.</summary>
    public const string SoftDeleteFilter = "SoftDelete";

    // SQL Server hands DATETIME2 back as DateTimeKind.Unspecified; mark UTC columns as UTC again on read.
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public static void ApplyCrossCuttingConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => !t.IsOwned()).ToList())
        {
            var clrType = entityType.ClrType;
            var entity = modelBuilder.Entity(clrType);

            if (typeof(AuditableEntity).IsAssignableFrom(clrType))
            {
                entity.Property(nameof(AuditableEntity.CreatedBy)).HasMaxLength(AuditableEntity.ActorMaxLength);
                entity.Property(nameof(AuditableEntity.UpdatedBy)).HasMaxLength(AuditableEntity.ActorMaxLength);
            }

            if (typeof(IHasPublicId).IsAssignableFrom(clrType))
            {
                // Generated in code as a random UUID, never by the database (NEWSEQUENTIALID is guessable).
                entity.Property(nameof(IHasPublicId.PublicId)).ValueGeneratedNever();
                entity.HasIndex(nameof(IHasPublicId.PublicId)).IsUnique();
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType) && entityType.BaseType is null)
            {
                entity.HasQueryFilter(SoftDeleteFilter, NotDeleted(clrType));
            }

            // Raised events live in memory until the unit of work dispatches them; they are never stored.
            if (typeof(IHasDomainEvents).IsAssignableFrom(clrType))
            {
                entity.Ignore(nameof(IHasDomainEvents.DomainEvents));
            }

            foreach (var property in entityType.GetProperties().Where(IsUtcInstant))
            {
                property.SetValueConverter(UtcConverter);
            }

            // No hard deletes (FR-1.5): deleting a parent must never cascade into its history.
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }

    private static bool IsUtcInstant(IMutableProperty property) =>
        (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
        && (property.Name.EndsWith("Utc", StringComparison.Ordinal)
            || property.Name is nameof(AuditableEntity.CreatedAt) or nameof(AuditableEntity.UpdatedAt));

    private static LambdaExpression NotDeleted(Type clrType)
    {
        var entity = Expression.Parameter(clrType, "e");
        var isDeleted = Expression.Property(entity, nameof(ISoftDeletable.IsDeleted));
        return Expression.Lambda(Expression.Not(isDeleted), entity);
    }
}
