namespace SGrowing.Api.Modules.Auth;

public record RegisterRequest(
    string OrganizationName,
    string? OrganizationSlug,
    string FullName,
    string Email,
    string Password
);

public record LoginRequest(
    string Email,
    string Password
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record UserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string FullName,
    string Role,
    string? AvatarUrl
);

public record TenantDto(
    Guid Id,
    string Name,
    string Slug
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    UserDto User,
    TenantDto Tenant
);

public record UserProfileResponse(
    UserDto User,
    TenantDto Tenant,
    List<string> Permissions
);
