namespace Attendance.Api.Contracts.Auth;

// Request bodies are separate from the commands: the client-IP and user-agent fields of a command come
// from the connection, and binding commands directly would let a caller set them.
// Each ToString is overridden to keep credentials out of logs and exception messages.

public sealed record LoginRequest(string Email, string Password)
{
    public override string ToString() => nameof(LoginRequest);
}

public sealed record RefreshTokenRequest(string RefreshToken)
{
    public override string ToString() => nameof(RefreshTokenRequest);
}

public sealed record LogoutRequest(string RefreshToken)
{
    public override string ToString() => nameof(LogoutRequest);
}
