using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Funnels.Autonomous;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Funnels;

public class FunnelSplitTestTests
{
    [Fact]
    public async Task CreateOrGetSplitTestAsync_InitializesWithCalibrationBaseline()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"FunnelSplitTestDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var funnelRunId = Guid.NewGuid();

        using var db = new SGrowingDbContext(options);
        var service = new FunnelSplitTestService(db);

        var req = new CreateFunnelSplitTestRequest(
            VariantA_Name: "Variant A (Direct Feature List)",
            VariantA_Headline: "Save 40% on Logistics Delivery Fees",
            VariantA_TrafficRatio: 50.0,
            VariantB_Name: "Variant B (Urgency & COD)",
            VariantB_Headline: "Get Risk-Free Doorstep Cash Collection Guaranteed",
            VariantB_TrafficRatio: 50.0
        );

        // Act
        var result = await service.CreateOrGetSplitTestAsync(tenantId, funnelRunId, req);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Active");
        result.VariantA_Name.Should().Be("Variant A (Direct Feature List)");
        result.VariantB_Name.Should().Be("Variant B (Urgency & COD)");
        result.VariantA_Views.Should().BeGreaterThan(0);
        result.VariantB_Views.Should().BeGreaterThan(0);
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(50.0);
    }

    [Fact]
    public async Task RecordEventAsync_Conversions_UpdatesMetricsAndCalculatesConfidence()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"FunnelSplitTestDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var funnelRunId = Guid.NewGuid();

        using var db = new SGrowingDbContext(options);
        var service = new FunnelSplitTestService(db);

        var req = new CreateFunnelSplitTestRequest(null, null, 50, null, null, 50);
        await service.CreateOrGetSplitTestAsync(tenantId, funnelRunId, req);

        // Act: Record 10 additional conversions for Variant B
        FunnelSplitTestSummaryDto? updated = null;
        for (int i = 0; i < 10; i++)
        {
            updated = await service.RecordEventAsync(tenantId, funnelRunId, new RecordSplitTestEventRequest("B", "conversion"));
        }

        // Assert
        updated.Should().NotBeNull();
        updated!.VariantB_Conversions.Should().Be(32); // Initial 22 + 10
        updated.VariantB_ConversionRate.Should().BeGreaterThan(updated.VariantA_ConversionRate);
        updated.ConversionRateDeltaPercent.Should().BeGreaterThan(0);
        updated.ConfidenceScore.Should().BeGreaterThan(90.0);
    }

    [Fact]
    public async Task ConcludeTestAsync_SetsStatusToConcluded()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"FunnelSplitTestDb_{Guid.NewGuid()}")
            .Options;

        var tenantId = Guid.NewGuid();
        var funnelRunId = Guid.NewGuid();

        using var db = new SGrowingDbContext(options);
        var service = new FunnelSplitTestService(db);

        await service.CreateOrGetSplitTestAsync(tenantId, funnelRunId, new CreateFunnelSplitTestRequest(null, null, null, null, null, null));

        // Act
        bool success = await service.ConcludeTestAsync(tenantId, funnelRunId, "Variant B (Urgency & COD Proof)");
        var test = await service.GetSplitTestAsync(tenantId, funnelRunId);

        // Assert
        success.Should().BeTrue();
        test.Should().NotBeNull();
        test!.Status.Should().Be("Concluded");
        test.WinnerVariant.Should().Be("Variant B (Urgency & COD Proof)");
    }
}
