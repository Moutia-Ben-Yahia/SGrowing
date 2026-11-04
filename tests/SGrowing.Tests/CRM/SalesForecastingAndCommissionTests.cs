using Microsoft.EntityFrameworkCore;
using Moq;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.CRM;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using Xunit;

namespace SGrowing.Tests.CRM;

public class SalesForecastingAndCommissionTests
{
    private readonly SGrowingDbContext _db;
    private readonly Mock<IOutbox> _outboxMock;
    private readonly SalesForecastingService _service;

    public SalesForecastingAndCommissionTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _outboxMock = new Mock<IOutbox>();
        _service = new SalesForecastingService(_db, _outboxMock.Object);
    }

    [Fact]
    public async Task GetForecastSummary_ComputesWeightedPipelineAndQuotaAttainment()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);
        var dealDate = new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);

        var period = await _service.CreateForecastPeriodAsync(tenantId, "Q1 2026", startDate, endDate, targetQuota: 100000m);

        // Add Deals across stages
        // 1. Won deal: $40,000 (weight 1.0 -> $40,000)
        var wonDeal = Deal.Create(tenantId, contactId, "Enterprise Deal A", 40000m, DealStage.Won, expectedCloseDate: dealDate);
        // 2. Proposal deal: $50,000 (weight 0.6 -> $30,000)
        var proposalDeal = Deal.Create(tenantId, contactId, "Mid-Market Deal B", 50000m, DealStage.Proposal, expectedCloseDate: dealDate);
        // 3. Qualified deal: $20,000 (weight 0.3 -> $6,000)
        var qualifiedDeal = Deal.Create(tenantId, contactId, "Startup Deal C", 20000m, DealStage.Qualified, expectedCloseDate: dealDate);
        // 4. Lost deal: $10,000 (weight 0.0 -> $0)
        var lostDeal = Deal.Create(tenantId, contactId, "Churned Deal D", 10000m, DealStage.Lost, expectedCloseDate: dealDate);

        await _db.Deals.AddRangeAsync(wonDeal, proposalDeal, qualifiedDeal, lostDeal);
        await _db.SaveChangesAsync();

        var summary = await _service.GetForecastSummaryAsync(tenantId, period.Id);

        Assert.NotNull(summary);
        Assert.Equal(40000m, summary.ClosedWonRevenue);
        // Unweighted pipeline excludes Won and Lost = 50,000 + 20,000 = 70,000
        Assert.Equal(70000m, summary.UnweightedPipelineRevenue);
        // Weighted total = 40,000 + 30,000 + 6,000 = 76,000
        Assert.Equal(76000m, summary.WeightedForecastRevenue);
        // Quota attainment = 40,000 / 100,000 = 40%
        Assert.Equal(40.0, summary.QuotaAttainmentPercentage);
        Assert.Equal(4, summary.TotalDealsCount);
    }

    [Fact]
    public async Task CalculateRepCommissions_WithAccelerator_CalculatesCorrectlyWhenExceedingQuota()
    {
        var tenantId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var startDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);
        var closeDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

        var period = await _service.CreateForecastPeriodAsync(tenantId, "Q2 2026", startDate, endDate, targetQuota: 50000m);
        // Plan: Base 10%, Accelerator 20% on surplus
        var plan = await _service.CreateCommissionPlanAsync(tenantId, "Standard Tech Sales Plan", baseRate: 10.0m, threshold: 100.0m, acceleratedRate: 20.0m);

        // Rep won $70,000 in deals ($20,000 over quota)
        var deal1 = Deal.Create(tenantId, contactId, "Expansion Pack", 40000m, DealStage.Won, ownerId: repId, expectedCloseDate: closeDate);
        var deal2 = Deal.Create(tenantId, contactId, "New Logo", 30000m, DealStage.Won, ownerId: repId, expectedCloseDate: closeDate);

        await _db.Deals.AddRangeAsync(deal1, deal2);
        await _db.SaveChangesAsync();

        var statement = await _service.CalculateRepCommissionsAsync(tenantId, period.Id, plan.Id, repId, "Alex Vance");

        Assert.NotNull(statement);
        Assert.Equal(70000m, statement.TotalDealsValue);
        Assert.Equal(2, statement.DealsClosedCount);
        // Base: 50,000 * 10% = 5,000. Surplus: 20,000 * 20% = 4,000. Total = 9,000.
        Assert.Equal(9000m, statement.CalculatedCommission);
        Assert.Equal(CommissionStatementStatus.Draft, statement.Status);
    }

    [Fact]
    public async Task ApproveCommissionStatement_UpdatesStatusAndDispatchesOutbox()
    {
        var tenantId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var period = await _service.CreateForecastPeriodAsync(tenantId, "Q3 2026", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), 10000m);
        var plan = await _service.CreateCommissionPlanAsync(tenantId, "Plan", 10m, 100m, 15m);
        var statement = await _service.CalculateRepCommissionsAsync(tenantId, period.Id, plan.Id, repId, "Jordan Belfort");

        var approved = await _service.ApproveCommissionStatementAsync(tenantId, statement.Id, "Fully audited by Finance Director");

        Assert.Equal(CommissionStatementStatus.Approved, approved.Status);
        Assert.NotNull(approved.ApprovedAtUtc);
        Assert.Equal("Fully audited by Finance Director", approved.Notes);

        _outboxMock.Verify(o => o.EnqueueAsync(
            tenantId,
            "Sales.CommissionApproved",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetForecastSummary_ThrowsKeyNotFoundException_WhenPeriodDoesNotExist()
    {
        var tenantId = Guid.NewGuid();
        var nonExistentPeriodId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetForecastSummaryAsync(tenantId, nonExistentPeriodId));
    }

    [Fact]
    public async Task GetForecastSummary_ZeroQuota_ReturnsZeroPercentAttainment()
    {
        var tenantId = Guid.NewGuid();
        var period = await _service.CreateForecastPeriodAsync(tenantId, "Zero Quota Test", DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 0m);

        var summary = await _service.GetForecastSummaryAsync(tenantId, period.Id);

        Assert.Equal(0.0, summary.QuotaAttainmentPercentage);
        Assert.Equal(0m, summary.ClosedWonRevenue);
    }

    [Fact]
    public async Task CalculateRepCommissions_WhenNotExceedingQuota_AppliesBaseRateWithoutAccelerator()
    {
        var tenantId = Guid.NewGuid();
        var repId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var startDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc);
        var closeDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var period = await _service.CreateForecastPeriodAsync(tenantId, "Q3 2026", startDate, endDate, targetQuota: 50000m);
        var plan = await _service.CreateCommissionPlanAsync(tenantId, "Plan", baseRate: 10.0m, threshold: 100.0m, acceleratedRate: 20.0m);

        // Rep won $30,000 in deals (under the $50,000 quota)
        var deal = Deal.Create(tenantId, contactId, "Mid Deal", 30000m, DealStage.Won, ownerId: repId, expectedCloseDate: closeDate);
        await _db.Deals.AddAsync(deal);
        await _db.SaveChangesAsync();

        var statement = await _service.CalculateRepCommissionsAsync(tenantId, period.Id, plan.Id, repId, "Sammy Sosa");

        Assert.NotNull(statement);
        Assert.Equal(30000m, statement.TotalDealsValue);
        // Base rate: 30,000 * 10% = 3,000
        Assert.Equal(3000m, statement.CalculatedCommission);
    }

    [Fact]
    public async Task CalculateRepCommissions_ThrowsKeyNotFoundException_WhenPeriodOrPlanMissing()
    {
        var tenantId = Guid.NewGuid();
        var validPeriod = await _service.CreateForecastPeriodAsync(tenantId, "Period", DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 1000m);

        // Missing plan
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CalculateRepCommissionsAsync(tenantId, validPeriod.Id, Guid.NewGuid(), Guid.NewGuid(), "Rep"));

        // Missing period
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CalculateRepCommissionsAsync(tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Rep"));
    }

    [Fact]
    public async Task ApproveCommissionStatement_ThrowsKeyNotFoundException_WhenStatementMissing()
    {
        var tenantId = Guid.NewGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ApproveCommissionStatementAsync(tenantId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetForecastPeriods_ReturnsPeriodsOrderedByStartDateDescending()
    {
        var tenantId = Guid.NewGuid();
        var p1 = await _service.CreateForecastPeriodAsync(tenantId, "P1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), 10000m);
        var p2 = await _service.CreateForecastPeriodAsync(tenantId, "P2", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), 15000m);

        var list = await _service.GetForecastPeriodsAsync(tenantId);

        Assert.Equal(2, list.Count);
        Assert.Equal("P2", list[0].Name);
        Assert.Equal("P1", list[1].Name);
    }
}
