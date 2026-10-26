using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(AttendanceDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Include(t => t.User)
            .ThenInclude(u => u.Employee)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetUnrevokedInFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedUtc == null)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetUnrevokedForUserAsync(long userId, CancellationToken cancellationToken) =>
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedUtc == null)
            .ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}
