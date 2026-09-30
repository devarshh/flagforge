using FlagForge.Domain;

namespace FlagForge.Application.Users;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    Role Role,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LockoutEndsAt)
{
    public static UserResponse From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new UserResponse(
            user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.MustChangePassword, user.CreatedAt, user.LastLoginAt, user.LockoutEndsAt);
    }
}

public sealed record UserListQuery(int Page = 1, int PageSize = 25);

public sealed record CreateUserRequest(string Email, string DisplayName, Role Role);

/// <summary>The temporary password is shown once; FlagForge does not send email.</summary>
public sealed record CreateUserResponse(UserResponse User, string TemporaryPassword);

/// <summary>Partial update: null fields are left unchanged.</summary>
public sealed record UpdateUserRequest(string? DisplayName = null, Role? Role = null, bool? IsActive = null);

public sealed record ResetPasswordResponse(string TemporaryPassword);
