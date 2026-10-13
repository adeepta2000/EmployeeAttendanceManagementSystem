using Attendance.Domain.Common;
using Attendance.Domain.Identity;
using Attendance.Domain.Organization;
using Attendance.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Attendance.Application.Tests.Persistence;

/// <summary>Checks the SQL Server model. No database is needed: the connection is never opened.</summary>
public sealed class AttendanceModelTests
{
    [Fact]
    public void Model_matches_the_latest_migration()
    {
        using var context = CreateContext();

        context.Database.HasPendingModelChanges()
            .Should().BeFalse("every model change needs a migration (dotnet ef migrations add)");
    }

    [Fact]
    public void Employees_has_the_indexes_named_in_the_spec()
    {
        using var context = CreateContext();
        var indexes = context.Model.FindEntityType(typeof(Employee))!.GetIndexes()
            .ToDictionary(i => i.GetDatabaseName()!, i => i.Properties.Select(p => p.Name).ToArray());

        indexes.Should().ContainKey("IX_Employees_DepartmentId_IsActive")
            .WhoseValue.Should().Equal(nameof(Employee.DepartmentId), nameof(Employee.IsActive));
        indexes.Should().ContainKey("IX_Employees_ReportingManagerId")
            .WhoseValue.Should().Equal(nameof(Employee.ReportingManagerId));
    }

    [Theory]
    [InlineData(typeof(User), nameof(User.Email))]
    [InlineData(typeof(Role), nameof(Role.Name))]
    [InlineData(typeof(Permission), nameof(Permission.Code))]
    [InlineData(typeof(RefreshToken), nameof(RefreshToken.TokenHash))]
    [InlineData(typeof(Department), nameof(Department.Code))]
    [InlineData(typeof(Designation), nameof(Designation.Code))]
    [InlineData(typeof(WorkLocation), nameof(WorkLocation.Code))]
    [InlineData(typeof(Employee), nameof(Employee.EmployeeCode))]
    public void Business_keys_are_unique(Type entity, string property)
    {
        using var context = CreateContext();

        context.Model.FindEntityType(entity)!.GetIndexes()
            .Should().Contain(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == property);
    }

    [Fact]
    public void Every_public_entity_has_a_unique_code_generated_PublicId()
    {
        using var context = CreateContext();
        var publicEntities = context.Model.GetEntityTypes()
            .Where(t => typeof(IHasPublicId).IsAssignableFrom(t.ClrType))
            .ToList();

        publicEntities.Should().NotBeEmpty();
        foreach (var entityType in publicEntities)
        {
            var publicId = entityType.FindProperty(nameof(IHasPublicId.PublicId))!;

            publicId.ValueGenerated.Should().Be(ValueGenerated.Never, entityType.DisplayName());
            entityType.GetIndexes().Should().Contain(
                i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0] == publicId,
                $"{entityType.DisplayName()} is looked up by PublicId");
        }
    }

    [Fact]
    public void No_foreign_key_cascades_deletes()
    {
        using var context = CreateContext();

        context.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys())
            .Should().OnlyContain(fk => fk.DeleteBehavior == DeleteBehavior.Restrict);
    }

    private static AttendanceDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseSqlServer("Server=model-only;Database=model-only")
            .Options);
}
