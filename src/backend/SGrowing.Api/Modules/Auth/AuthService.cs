using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress = null);
    Task<UserProfileResponse> GetCurrentUserProfileAsync(Guid userId, Guid tenantId);
}

public class AuthService : IAuthService
{
    private readonly SGrowingDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public AuthService(SGrowingDbContext db, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.", nameof(request.Email));
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters long.", nameof(request.Password));
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
            throw new ArgumentException("Organization name is required.", nameof(request.OrganizationName));

        string normalizedEmail = request.Email.Trim().ToLowerInvariant();
        bool exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (exists)
            throw new InvalidOperationException($"User with email '{request.Email}' already exists.");

        string slug = string.IsNullOrWhiteSpace(request.OrganizationSlug)
            ? request.OrganizationName.ToLowerInvariant().Replace(" ", "-").Trim()
            : request.OrganizationSlug.Trim().ToLowerInvariant();

        // 1. Create Tenant
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.OrganizationName.Trim(),
            Slug = slug,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };
        _db.Tenants.Add(tenant);

        // 2. Initialize Default Roles for this Tenant
        var defaultRoles = new[]
        {
            SystemRole.CreateDefault(tenant.Id, UserSystemRole.SuperAdmin),
            SystemRole.CreateDefault(tenant.Id, UserSystemRole.OperationsManager),
            SystemRole.CreateDefault(tenant.Id, UserSystemRole.CallCenterAgent),
            SystemRole.CreateDefault(tenant.Id, UserSystemRole.WarehousePicker),
            SystemRole.CreateDefault(tenant.Id, UserSystemRole.FinanceAuditor),
            SystemRole.CreateDefault(tenant.Id, UserSystemRole.AffiliateCreator)
        };
        _db.SystemRoles.AddRange(defaultRoles);

        // 3. Hash Password & Create User
        var (hash, salt) = _hasher.HashPassword(request.Password);
        string refreshToken = _jwt.GenerateRefreshToken();

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            Role = UserRole.Admin,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            LastLoginAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Users.Add(user);

        // 4. Audit Log
        _db.SecurityAuditLogEntries.Add(new SecurityAuditLogEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            UserEmail = user.Email,
            UserRole = user.Role.ToString(),
            Action = "UserRegistration",
            Resource = "Tenant",
            ResourceId = tenant.Id.ToString(),
            Details = $"Organization '{tenant.Name}' and root Admin '{user.Email}' created.",
            IpAddress = ipAddress ?? "127.0.0.1",
            TimestampUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        string accessToken = _jwt.GenerateAccessToken(user, tenant.Name);
        var userDto = new UserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role.ToString(), user.AvatarUrl);
        var tenantDto = new TenantDto(tenant.Id, tenant.Name, tenant.Slug);

        return new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(60),
            userDto,
            tenantDto
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Email and password are required.");

        string normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user == null || !_hasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("User account is inactive. Please contact organization administrator.");

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == user.TenantId);

        if (tenant == null || !tenant.IsActive)
            throw new UnauthorizedAccessException("Organization account is inactive.");

        // Rotate Refresh Token
        string newRefreshToken = _jwt.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(30);
        user.LastLoginAtUtc = DateTime.UtcNow;

        _db.SecurityAuditLogEntries.Add(new SecurityAuditLogEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            UserEmail = user.Email,
            UserRole = user.Role.ToString(),
            Action = "UserLogin",
            Resource = "User",
            ResourceId = user.Id.ToString(),
            Details = $"Successful login from IP {ipAddress ?? "unknown"}.",
            IpAddress = ipAddress ?? "127.0.0.1",
            TimestampUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        string accessToken = _jwt.GenerateAccessToken(user, tenant.Name);
        var userDto = new UserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role.ToString(), user.AvatarUrl);
        var tenantDto = new TenantDto(tenant.Id, tenant.Name, tenant.Slug);

        return new AuthResponse(
            accessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(60),
            userDto,
            tenantDto
        );
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new ArgumentException("Refresh token is required.", nameof(request.RefreshToken));

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);

        if (user == null || user.RefreshTokenExpiresAtUtc < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("User account is deactivated.");

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == user.TenantId);

        if (tenant == null || !tenant.IsActive)
            throw new UnauthorizedAccessException("Tenant is inactive.");

        string newRefreshToken = _jwt.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(30);

        await _db.SaveChangesAsync();

        string accessToken = _jwt.GenerateAccessToken(user, tenant.Name);
        var userDto = new UserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role.ToString(), user.AvatarUrl);
        var tenantDto = new TenantDto(tenant.Id, tenant.Name, tenant.Slug);

        return new AuthResponse(
            accessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(60),
            userDto,
            tenantDto
        );
    }

    public async Task<UserProfileResponse> GetCurrentUserProfileAsync(Guid userId, Guid tenantId)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId);

        if (user == null)
            throw new InvalidOperationException($"User {userId} not found.");

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found.");

        var roles = await _db.SystemRoles
            .Where(r => r.TenantId == tenantId)
            .ToListAsync();

        var permissions = new List<string> { "ViewDashboard" };
        if (user.Role == UserRole.Admin)
        {
            permissions.AddRange(new[] { "AdminFullAccess", "ViewRawPii", "ExportData", "OverridePricing", "ApproveDisbursements" });
        }
        else
        {
            permissions.AddRange(new[] { "ViewOrders", "ProcessShipments", "ConfirmCalls" });
        }

        var userDto = new UserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role.ToString(), user.AvatarUrl);
        var tenantDto = new TenantDto(tenant.Id, tenant.Name, tenant.Slug);

        return new UserProfileResponse(userDto, tenantDto, permissions);
    }
}
