using Attendance.Application.Common.Exceptions;
using Attendance.Domain.Identity;
using Attendance.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Application.Tests.Persistence;

/// <summary>Two requests read the same row, both change it, and the second save must fail.</summary>
public sealed class ConcurrencyTokenTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 11, 4, 0, 0, DateTimeKind.Utc);

    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Only_one_of_two_concurrent_rotations_of_a_refresh_token_can_save()
    {
        var tokenId = await SeedRefreshTokenAsync();
        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var firstRead = await first.RefreshTokens.SingleAsync(t => t.Id == tokenId, Ct);
        var secondRead = await second.RefreshTokens.SingleAsync(t => t.Id == tokenId, Ct);

        firstRead.Revoke(Now, replacedByTokenHash: "FIRST");
        await new UnitOfWork(first, new RecordingDomainEventDispatcher()).SaveChangesAsync(Ct);
        secondRead.Revoke(Now, replacedByTokenHash: "SECOND");
        var act = () => new UnitOfWork(second, new RecordingDomainEventDispatcher()).SaveChangesAsync(Ct);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        await using var verify = _db.CreateContext();
        (await verify.RefreshTokens.SingleAsync(Ct)).ReplacedByTokenHash.Should().Be("FIRST");
    }

    [Fact]
    public async Task Concurrent_failed_sign_ins_conflict_instead_of_losing_a_count()
    {
        var userId = await SeedUserAsync();
        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var firstRead = await first.Users.SingleAsync(u => u.Id == userId, Ct);
        var secondRead = await second.Users.SingleAsync(u => u.Id == userId, Ct);

        firstRead.RegisterFailedLogin(Now, maxFailedAttempts: 5, TimeSpan.FromMinutes(15));
        await new UnitOfWork(first, new RecordingDomainEventDispatcher()).SaveChangesAsync(Ct);
        secondRead.RegisterFailedLogin(Now, maxFailedAttempts: 5, TimeSpan.FromMinutes(15));
        var act = () => new UnitOfWork(second, new RecordingDomainEventDispatcher()).SaveChangesAsync(Ct);

        await act.Should().ThrowAsync<ConcurrencyConflictException>("both read a count of 0 and would each write 1");
    }

    private async Task<long> SeedUserAsync()
    {
        await using var context = _db.CreateContext();
        var user = new User { Email = "tanvir.hossain@nordwin.example", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task<long> SeedRefreshTokenAsync()
    {
        var userId = await SeedUserAsync();
        await using var context = _db.CreateContext();
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = "HASH",
            FamilyId = Guid.NewGuid(),
            ExpiresUtc = Now.AddDays(7),
            CreatedByIp = "10.0.0.1",
        };
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync(Ct);
        return token.Id;
    }
}
