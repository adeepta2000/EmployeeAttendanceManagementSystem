using Attendance.Domain.Organization;
using Attendance.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Application.Tests.Persistence;

public sealed class AuditableEntityInterceptorTests : IDisposable
{
    private const string UserId = "6b1f0c8e-2a4d-4f7b-9c3e-5d8a7b6c4e21";

    private readonly SqliteTestDatabase _db = new();

    public AuditableEntityInterceptorTests() => _db.CurrentUser.UserId = UserId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Insert_stamps_the_created_columns_and_leaves_the_updated_columns_null()
    {
        await AddDesignationAsync();

        await using var verify = _db.CreateContext();
        var saved = await verify.Designations.SingleAsync(Ct);

        saved.CreatedAt.Should().Be(_db.Clock.Now.UtcDateTime);
        saved.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        saved.CreatedBy.Should().Be(UserId);
        saved.UpdatedAt.Should().BeNull();
        saved.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public async Task Update_stamps_the_updated_columns_and_keeps_the_created_columns()
    {
        var createdAt = _db.Clock.Now.UtcDateTime;
        await AddDesignationAsync();

        _db.Clock.Now = _db.Clock.Now.AddHours(2);
        _db.CurrentUser.UserId = "a-different-user";
        await using (var context = _db.CreateContext())
        {
            var designation = await context.Designations.SingleAsync(Ct);
            designation.Name = "Senior Sewing Operator";
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = _db.CreateContext();
        var saved = await verify.Designations.SingleAsync(Ct);

        saved.CreatedAt.Should().Be(createdAt);
        saved.CreatedBy.Should().Be(UserId);
        saved.UpdatedAt.Should().Be(_db.Clock.Now.UtcDateTime);
        saved.UpdatedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        saved.UpdatedBy.Should().Be("a-different-user");
    }

    [Fact]
    public async Task Without_a_signed_in_user_the_actor_is_system()
    {
        _db.CurrentUser.UserId = null;

        await AddDesignationAsync();

        await using var verify = _db.CreateContext();
        var saved = await verify.Designations.SingleAsync(Ct);
        saved.CreatedBy.Should().Be(AuditableEntityInterceptor.SystemActor);
    }

    [Fact]
    public async Task Removing_a_soft_deletable_entity_flags_the_row_instead_of_deleting_it()
    {
        await using (var context = _db.CreateContext())
        {
            context.SoftDeletableProbes.Add(new SoftDeletableProbe { Name = "probe" });
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = _db.CreateContext())
        {
            var probe = await context.SoftDeletableProbes.SingleAsync(Ct);
            context.SoftDeletableProbes.Remove(probe);
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = _db.CreateContext();
        (await verify.SoftDeletableProbes.AnyAsync(Ct)).Should().BeFalse("the soft-delete filter hides deleted rows");

        var row = await verify.SoftDeletableProbes.IgnoreQueryFilters().SingleAsync(Ct);
        row.IsDeleted.Should().BeTrue();
        row.UpdatedBy.Should().Be(UserId);
    }

    private async Task AddDesignationAsync()
    {
        await using var context = _db.CreateContext();
        context.Designations.Add(new Designation { Code = "SEW-OP", Name = "Sewing Operator" });
        await context.SaveChangesAsync(Ct);
    }
}
