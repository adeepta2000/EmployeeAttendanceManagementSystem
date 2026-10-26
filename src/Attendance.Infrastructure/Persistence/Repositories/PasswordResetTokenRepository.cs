using Attendance.Application.Identity.Abstractions;
using Attendance.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Attendance.Infrastructure.Persistence.Repositories;

internal sealed class PasswordResetTokenRepository(AttendanceDbContext db) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.PasswordResetTokens
            .Include(t => t.User)
            .ThenInclude(u => u.Employee)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<PasswordResetToken>> GetOutstandingForUserAsync(long userId, CancellationToken cancellationToken) =>
        await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedUtc == null && t.RevokedUtc == null)
            .ToListAsync(cancellationToken);

    public void Add(PasswordResetToken token) => db.PasswordResetTokens.Add(token);
}
