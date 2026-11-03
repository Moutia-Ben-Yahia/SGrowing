using Microsoft.AspNetCore.Http;
using Moq;
using SGrowing.Api.Modules.Billing;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Billing;

public class PlanQuotaEnforcementTests
{
    private readonly Mock<IPlanQuotaEnforcementService> _quotaServiceMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();

    [Fact]
    public async Task InvokeAsync_NoUsageMetricHeader_PassesThrough()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new PlanQuotaEnforcementMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_HardBlockedQuota_Returns403ForbiddenWithJsonError()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "AiTokens";
        context.Request.Headers["X-Requested-Quantity"] = "5000";

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var blockedResult = new PlanQuotaEnforcementResult
        {
            Allowed = false,
            IsHardBlocked = true,
            RequiresOverageWarning = false,
            PlanTier = PlanTier.Starter,
            Metric = UsageMetricType.AiTokens,
            CurrentUsage = 500000,
            RequestedQuantity = 5000,
            QuotaLimit = 500000,
            Message = "AI Token limit exhausted for Starter plan."
        };

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(tenantId, UsageMetricType.AiTokens, 5000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockedResult);

        var middleware = new PlanQuotaEnforcementMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_SoftLimitWithOverage_AppendsWarningHeaderAndPassesThrough()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "VoiceMinutes";

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var overageResult = new PlanQuotaEnforcementResult
        {
            Allowed = true,
            IsHardBlocked = false,
            RequiresOverageWarning = true,
            PlanTier = PlanTier.Growth,
            Metric = UsageMetricType.VoiceMinutes,
            CurrentUsage = 505,
            RequestedQuantity = 1,
            QuotaLimit = 500,
            Message = "Growth plan soft quota reached. Overages applied."
        };

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(tenantId, UsageMetricType.VoiceMinutes, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(overageResult);

        var middleware = new PlanQuotaEnforcementMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        // Assert
        Assert.True(nextCalled);
        Assert.True(context.Response.Headers.ContainsKey("X-Quota-Warning"));
        Assert.Contains("Overages applied", context.Response.Headers["X-Quota-Warning"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_ResolvesTenantFromXTenantIdHeader_WhenTenantContextEmpty()
    {
        // Arrange
        var headerTenantId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.TenantId).Returns(Guid.Empty);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "MonthlyColdEmails";
        context.Request.Headers["X-Tenant-Id"] = headerTenantId.ToString();

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var allowedResult = new PlanQuotaEnforcementResult
        {
            Allowed = true,
            IsHardBlocked = false,
            RequiresOverageWarning = false,
            PlanTier = PlanTier.Growth,
            Metric = UsageMetricType.MonthlyColdEmails,
            CurrentUsage = 250,
            RequestedQuantity = 1,
            QuotaLimit = 10000,
            Message = "Quota normal"
        };

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(headerTenantId, UsageMetricType.MonthlyColdEmails, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowedResult);

        var middleware = new PlanQuotaEnforcementMiddleware(next);

        // Act
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        // Assert
        Assert.True(nextCalled);
        _quotaServiceMock.Verify(s => s.CheckQuotaAsync(headerTenantId, UsageMetricType.MonthlyColdEmails, 1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
