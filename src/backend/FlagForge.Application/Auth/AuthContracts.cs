using FlagForge.Application.Users;

namespace FlagForge.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>A new session: the response body plus the refresh token the API puts in the httpOnly cookie.</summary>
public sealed record AuthSession(LoginResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
