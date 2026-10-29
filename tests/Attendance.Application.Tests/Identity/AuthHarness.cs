using Attendance.Application.Common.Interfaces;
using Attendance.Application.Identity;
using Attendance.Application.Identity.Abstractions;
using Attendance.Application.Identity.Login;
using Attendance.Application.Identity.Logout;
using Attendance.Application.Identity.Refresh;
using Attendance.Application.Tests.Persistence;
using Attendance.Domain.Identity;
using Attendance.Infrastructure.Identity;
using Attendance.Infrastructure.Persistence;
using Attendance.Infrastructure.Persistence.Repositories;
using Attendance.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Attendance.Application.Tests.Identity;

/// <summary>
/// The real auth handlers, repositories and token services over a SQLite database. Every call gets
/// its own DbContext, the way separate HTTP requests do.
/// </summary>
internal sealed class AuthHarness : IDisposable
{
    public const string Password = "Correct-Horse-Battery-9";
    public const string ClientIp = "10.20.30.40";
    public const string UserAgent = "AuthHarness/1.0";

    private readonly SqliteTestDatabase _db = new();
    private readonly IOptions<AuthOptions> _authOptions = Options.Create(new AuthOptions());
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "test-signing-key-that-is-at-least-32-characters",
    });

    public TestClock Clock => _db.Clock;

    public RefreshTokenGenerator TokenGenerator { get; } = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    public TestDbContext CreateContext() => _db.CreateContext();

    public async Task<User> CreateUserAsync(string email)
    {
        await using var context = _db.CreateContext();
        var user = new User { Email = email, PasswordHash = new IdentityPasswordHasher().Hash(Password) };
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user;
    }

    public async Task<Result<AuthTokens>> LoginAsync(string email, string password = Password)
    {
        await using var context = _db.CreateContext();
        var unitOfWork = new UnitOfWork(context, new RecordingDomainEventDispatcher());
        var handler = new LoginCommandHandler(
            new UserRepository(context),
            new IdentityPasswordHasher(),
            CreateSessions(context, new RefreshTokenRepository(context), unitOfWork),
            unitOfWork,
            _authOptions,
            Clock,
            NullLogger<LoginCommandHandler>.Instance);

        return await handler.HandleAsync(new LoginCommand(email, password, ClientIp, UserAgent), Ct);
    }

    /// <param name="wrapRepository">Lets a test intercept the handler's repository, e.g. to stage a race.</param>
    public async Task<Result<AuthTokens>> RefreshAsync(
        string refreshToken,
        Func<IRefreshTokenRepository, IRefreshTokenRepository>? wrapRepository = null)
    {
        await using var context = _db.CreateContext();
        var repository = new RefreshTokenRepository(context);
        var refreshTokens = wrapRepository?.Invoke(repository) ?? repository;
        var unitOfWork = new UnitOfWork(context, new RecordingDomainEventDispatcher());
        var handler = new RefreshTokenCommandHandler(
            refreshTokens,
            TokenGenerator,
            CreateSessions(context, refreshTokens, unitOfWork),
            unitOfWork,
            Clock,
            NullLogger<RefreshTokenCommandHandler>.Instance);

        return await handler.HandleAsync(new RefreshTokenCommand(refreshToken, ClientIp, UserAgent), Ct);
    }

    public async Task<Result> LogoutAsync(string refreshToken)
    {
        await using var context = _db.CreateContext();
        var refreshTokens = new RefreshTokenRepository(context);
        var handler = new LogoutCommandHandler(
            refreshTokens,
            TokenGenerator,
            CreateSessions(context, refreshTokens, new UnitOfWork(context, new RecordingDomainEventDispatcher())),
            Clock,
            NullLogger<LogoutCommandHandler>.Instance);

        return await handler.HandleAsync(new LogoutCommand(refreshToken), Ct);
    }

    /// <summary>The stored row for a raw token, read fresh from the database.</summary>
    public async Task<RefreshToken> GetStoredAsync(string refreshToken)
    {
        var hash = TokenGenerator.Hash(refreshToken);
        await using var context = _db.CreateContext();
        return await context.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash, Ct);
    }

    public async Task<List<RefreshToken>> GetFamilyAsync(Guid familyId)
    {
        await using var context = _db.CreateContext();
        return await context.RefreshTokens.AsNoTracking().Where(t => t.FamilyId == familyId).ToListAsync(Ct);
    }

    private AuthSessionService CreateSessions(
        AttendanceDbContext context,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork) =>
        new(
            new UserRepository(context),
            refreshTokens,
            new JwtAccessTokenIssuer(_jwtOptions, _authOptions),
            TokenGenerator,
            unitOfWork,
            _authOptions);
}
