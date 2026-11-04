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

    [Fact]
    public async Task InvokeAsync_InvalidUsageMetricHeader_PassesThrough()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "UnknownNonExistentMetric";
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new PlanQuotaEnforcementMiddleware(next);
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        _quotaServiceMock.Verify(s => s.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UsageMetricType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_TenantIdFromQueryParam_ExecutesQuotaCheck()
    {
        var queryTenantId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.TenantId).Returns(Guid.Empty);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "StorageMb";
        context.Request.QueryString = new QueryString($"?tenantId={queryTenantId}");

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
            Metric = UsageMetricType.StorageMb,
            CurrentUsage = 100,
            RequestedQuantity = 1,
            QuotaLimit = 1000,
            Message = "Storage available"
        };

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(queryTenantId, UsageMetricType.StorageMb, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowedResult);

        var middleware = new PlanQuotaEnforcementMiddleware(next);
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        Assert.True(nextCalled);
        _quotaServiceMock.Verify(s => s.CheckQuotaAsync(queryTenantId, UsageMetricType.StorageMb, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_EmptyTenantIdEverywhere_BypassesQuotaCheckAndCallsNext()
    {
        _tenantContextMock.Setup(t => t.TenantId).Returns(Guid.Empty);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "VoiceMinutes";

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new PlanQuotaEnforcementMiddleware(next);
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        Assert.True(nextCalled);
        _quotaServiceMock.Verify(s => s.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UsageMetricType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_NegativeOrZeroRequestedQuantity_ClampsToOne()
    {
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "AiTokens";
        context.Request.Headers["X-Requested-Quantity"] = "-50";

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
            PlanTier = PlanTier.Enterprise,
            Metric = UsageMetricType.AiTokens,
            CurrentUsage = 1000,
            RequestedQuantity = 1,
            QuotaLimit = 1000000,
            Message = "Within quota"
        };

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(tenantId, UsageMetricType.AiTokens, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowedResult);

        var middleware = new PlanQuotaEnforcementMiddleware(next);
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        Assert.True(nextCalled);
        // Verify clamped to 1
        _quotaServiceMock.Verify(s => s.CheckQuotaAsync(tenantId, UsageMetricType.AiTokens, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_NonNumericRequestedQuantity_DefaultsToOne()
    {
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "CarrierWaybills";
        context.Request.Headers["X-Requested-Quantity"] = "not_a_number";

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
            PlanTier = PlanTier.Starter,
            Metric = UsageMetricType.CarrierWaybills,
            CurrentUsage = 10,
            RequestedQuantity = 1,
            QuotaLimit = 50,
            Message = "Waybill available"
        };

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(tenantId, UsageMetricType.CarrierWaybills, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowedResult);

        var middleware = new PlanQuotaEnforcementMiddleware(next);
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        Assert.True(nextCalled);
        _quotaServiceMock.Verify(s => s.CheckQuotaAsync(tenantId, UsageMetricType.CarrierWaybills, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_AllowedWithoutWarning_DoesNotAddWarningHeaderAndCallsNext()
    {
        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Usage-Metric"] = "OutboundMessages";

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
            Metric = UsageMetricType.OutboundMessages,
            CurrentUsage = 50,
            RequestedQuantity = 1,
            QuotaLimit = 5000,
            Message = "Within quota"
        };

        _quotaServiceMock
            .Setup(s => s.CheckQuotaAsync(tenantId, UsageMetricType.OutboundMessages, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowedResult);

        var middleware = new PlanQuotaEnforcementMiddleware(next);
        await middleware.InvokeAsync(context, _quotaServiceMock.Object, _tenantContextMock.Object);

        Assert.True(nextCalled);
        Assert.False(context.Response.Headers.ContainsKey("X-Quota-Warning"));
    }
}
