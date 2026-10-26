using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Auth;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Security;

public class AuthTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuthDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    private IConfiguration CreateConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:SecretKey", "TestSecretKey_ForEnterpriseAuthTesting_1234567890!!" },
            { "Jwt:Issuer", "SGrowing.TestApi" },
            { "Jwt:Audience", "SGrowing.TestClients" },
            { "Jwt:ExpiryMinutes", "30" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public void PasswordHasher_Should_Hash_And_Verify_Correctly()
    {
        var hasher = new Pbkdf2PasswordHasher();
        string rawPassword = "StrongEnterprisePassword!2026";

        var (hash, salt) = hasher.HashPassword(rawPassword);

        Assert.NotEmpty(hash);
        Assert.NotEmpty(salt);
        Assert.True(hasher.VerifyPassword(rawPassword, hash, salt));
        Assert.False(hasher.VerifyPassword("WrongPassword!123", hash, salt));
    }

    [Fact]
    public void JwtTokenService_Should_Generate_And_Validate_Tokens()
    {
        var config = CreateConfiguration();
        var jwt = new JwtTokenService(config);

        var tenantId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "admin@sgrowing.io",
            FullName = "Master Admin",
            Role = UserRole.Admin
        };

        string token = jwt.GenerateAccessToken(user, "SGrowing Global");
        Assert.NotEmpty(token);

        var principal = jwt.ValidateToken(token);
        Assert.NotNull(principal);

        var subClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
        var tenantClaim = principal.FindFirst("tenant_id")?.Value;
        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value
            ?? principal.FindFirst("role")?.Value;

        Assert.Equal(user.Id.ToString(), subClaim);
        Assert.Equal(user.Email, emailClaim);
        Assert.Equal(tenantId.ToString(), tenantClaim);
        Assert.Equal("Admin", roleClaim);
    }

    [Fact]
    public async Task Register_And_Login_Flow_Should_Succeed_With_Audit_Trail()
    {
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = new JwtTokenService(config);
        var authService = new AuthService(db, hasher, jwt);

        // 1. Register New Tenant & Admin User
        var registerReq = new RegisterRequest(
            OrganizationName: "Atlas Commerce Group",
            OrganizationSlug: "atlas-commerce",
            FullName: "Karim Ben Salah",
            Email: "karim@atlas.com",
            Password: "SecurePassword999!"
        );

        var registerRes = await authService.RegisterAsync(registerReq, ipAddress: "197.1.2.3");

        Assert.NotNull(registerRes.AccessToken);
        Assert.NotNull(registerRes.RefreshToken);
        Assert.Equal("karim@atlas.com", registerRes.User.Email);
        Assert.Equal("Atlas Commerce Group", registerRes.Tenant.Name);

        // Verify Default Roles were generated for tenant
        var roles = await db.SystemRoles.Where(r => r.TenantId == registerRes.Tenant.Id).ToListAsync();
        Assert.Equal(6, roles.Count);

        // Verify Audit Log
        var audit = await db.SecurityAuditLogEntries
            .FirstOrDefaultAsync(a => a.TenantId == registerRes.Tenant.Id && a.Action == "UserRegistration");
        Assert.NotNull(audit);
        Assert.Equal("197.1.2.3", audit.IpAddress);

        // 2. Login with Valid Credentials
        var loginReq = new LoginRequest("karim@atlas.com", "SecurePassword999!");
        var loginRes = await authService.LoginAsync(loginReq, ipAddress: "197.1.2.4");

        Assert.NotNull(loginRes.AccessToken);
        Assert.NotEqual(registerRes.RefreshToken, loginRes.RefreshToken); // Rotated refresh token

        // 3. Login with Invalid Password Should Throw UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authService.LoginAsync(new LoginRequest("karim@atlas.com", "WrongPassword!")));

        // 4. Refresh Token Flow
        var refreshRes = await authService.RefreshTokenAsync(new RefreshTokenRequest(loginRes.RefreshToken));
        Assert.NotNull(refreshRes.AccessToken);
        Assert.NotNull(refreshRes.RefreshToken);

        // 5. Get Profile
        var profile = await authService.GetCurrentUserProfileAsync(loginRes.User.Id, loginRes.Tenant.Id);
        Assert.Equal("karim@atlas.com", profile.User.Email);
        Assert.Contains("AdminFullAccess", profile.Permissions);
    }

    [Fact]
    public async Task TenantContextMiddleware_Should_Extract_TenantId_From_Claims_Or_Header()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext();

        var httpContext = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("sub", Guid.NewGuid().ToString())
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, tenantContext);

        Assert.True(tenantContext.HasTenant);
        Assert.Equal(tenantId, tenantContext.TenantId);

        // Test Fallback with X-Tenant-Id header
        var headerTenantId = Guid.NewGuid();
        var headerTenantContext = new TenantContext();
        var headerHttpContext = new DefaultHttpContext();
        headerHttpContext.Request.Headers["X-Tenant-Id"] = headerTenantId.ToString();

        await middleware.InvokeAsync(headerHttpContext, headerTenantContext);

        Assert.True(headerTenantContext.HasTenant);
        Assert.Equal(headerTenantId, headerTenantContext.TenantId);
    }
}
