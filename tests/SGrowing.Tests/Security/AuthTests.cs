using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
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

        hash.Should().NotBeEmpty();
        salt.Should().NotBeEmpty();
        hasher.VerifyPassword(rawPassword, hash, salt).Should().BeTrue();
        hasher.VerifyPassword("WrongPassword!123", hash, salt).Should().BeFalse();
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
        token.Should().NotBeEmpty();

        var principal = jwt.ValidateToken(token);
        principal.Should().NotBeNull();

        var subClaim = principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
        var tenantClaim = principal.FindFirst("tenant_id")?.Value;
        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value
            ?? principal.FindFirst("role")?.Value;

        subClaim.Should().Be(user.Id.ToString());
        emailClaim.Should().Be(user.Email);
        tenantClaim.Should().Be(tenantId.ToString());
        roleClaim.Should().Be("Admin");
    }

    [Fact]
    public void JwtTokenService_ValidateToken_InvalidSignature_ReturnsNull()
    {
        // Arrange
        var config1 = CreateConfiguration();
        var jwt1 = new JwtTokenService(config1);

        var config2 = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Jwt:SecretKey", "DifferentSecretKey_ForTestingInvalidSignature_1234!!" },
            { "Jwt:Issuer", "SGrowing.Different" },
            { "Jwt:Audience", "SGrowing.Different" }
        }).Build();
        var jwt2 = new JwtTokenService(config2);

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "tamper@sgrowing.io",
            FullName = "Tampered User",
            Role = UserRole.Agent
        };

        string token = jwt1.GenerateAccessToken(user, "TestOrg");

        // Act: validate with mismatched secret
        var principal = jwt2.ValidateToken(token);

        // Assert
        principal.Should().BeNull();
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

        registerRes.AccessToken.Should().NotBeNull();
        registerRes.RefreshToken.Should().NotBeNull();
        registerRes.User.Email.Should().Be("karim@atlas.com");
        registerRes.Tenant.Name.Should().Be("Atlas Commerce Group");

        // Verify Default Workspace was generated for tenant and assigned to user
        var defaultWs = await db.Workspaces.FirstOrDefaultAsync(w => w.TenantId == registerRes.Tenant.Id);
        defaultWs.Should().NotBeNull();
        defaultWs!.Name.Should().Be("Main Workspace");
        registerRes.User.WorkspaceId.Should().Be(defaultWs.Id);

        var regClaims = jwt.ValidateToken(registerRes.AccessToken);
        regClaims.Should().NotBeNull();
        regClaims!.FindFirst("workspace_id")?.Value.Should().Be(defaultWs.Id.ToString());

        // Verify Default Roles were generated for tenant
        var roles = await db.SystemRoles.Where(r => r.TenantId == registerRes.Tenant.Id).ToListAsync();
        roles.Should().HaveCount(6);

        // Verify Audit Log
        var audit = await db.SecurityAuditLogEntries
            .FirstOrDefaultAsync(a => a.TenantId == registerRes.Tenant.Id && a.Action == "UserRegistration");
        audit.Should().NotBeNull();
        audit!.IpAddress.Should().Be("197.1.2.3");

        // 2. Login with Valid Credentials
        var loginReq = new LoginRequest("karim@atlas.com", "SecurePassword999!");
        var loginRes = await authService.LoginAsync(loginReq, ipAddress: "197.1.2.4");

        loginRes.AccessToken.Should().NotBeNull();
        loginRes.RefreshToken.Should().NotBe(registerRes.RefreshToken); // Rotated refresh token

        // 3. Login with Invalid Password Should Throw UnauthorizedAccessException
        var actInvalidPassword = () => authService.LoginAsync(new LoginRequest("karim@atlas.com", "WrongPassword!"));
        await actInvalidPassword.Should().ThrowAsync<UnauthorizedAccessException>();

        // 4. Refresh Token Flow
        var refreshRes = await authService.RefreshTokenAsync(new RefreshTokenRequest(loginRes.RefreshToken));
        refreshRes.AccessToken.Should().NotBeNull();
        refreshRes.RefreshToken.Should().NotBeNull();

        // 5. Get Profile
        var profile = await authService.GetCurrentUserProfileAsync(loginRes.User.Id, loginRes.Tenant.Id);
        profile.User.Email.Should().Be("karim@atlas.com");
        profile.Permissions.Should().Contain("AdminFullAccess");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = new JwtTokenService(config);
        var authService = new AuthService(db, hasher, jwt);

        var req1 = new RegisterRequest("Org 1", "org-1", "User 1", "duplicate@test.com", "Pass123456!");
        await authService.RegisterAsync(req1);

        var req2 = new RegisterRequest("Org 2", "org-2", "User 2", "duplicate@test.com", "Pass123456!");

        // Act
        var act = () => authService.RegisterAsync(req2);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Login_NonExistentEmail_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var db = CreateDbContext();
        var config = CreateConfiguration();
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = new JwtTokenService(config);
        var authService = new AuthService(db, hasher, jwt);

        // Act
        var act = () => authService.LoginAsync(new LoginRequest("ghost@nonexistent.io", "SomePassword"));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
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

        tenantContext.HasTenant.Should().BeTrue();
        tenantContext.TenantId.Should().Be(tenantId);

        // Test Fallback with X-Tenant-Id header
        var headerTenantId = Guid.NewGuid();
        var headerTenantContext = new TenantContext();
        var headerHttpContext = new DefaultHttpContext();
        headerHttpContext.Request.Headers["X-Tenant-Id"] = headerTenantId.ToString();

        await middleware.InvokeAsync(headerHttpContext, headerTenantContext);

        headerTenantContext.HasTenant.Should().BeTrue();
        headerTenantContext.TenantId.Should().Be(headerTenantId);
    }

    [Fact]
    public async Task TenantContextMiddleware_NoTenantHeaderOrClaim_LeavesHasTenantFalse()
    {
        // Arrange
        var tenantContext = new TenantContext();
        var httpContext = new DefaultHttpContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(httpContext, tenantContext);

        // Assert
        tenantContext.HasTenant.Should().BeFalse();
        tenantContext.TenantId.Should().Be(Guid.Empty);
    }
}
