using FluentAssertions;
using SGrowing.Api.Modules.Retention.CustomerHealth;
using Xunit;

namespace SGrowing.Tests.Unit;

public class CustomerHealthTests
{
    private readonly CustomerHealthService _service;
    private readonly Guid _tenantId = CustomerHealthService.DefaultTenantId;

    public CustomerHealthTests()
    {
        _service = new CustomerHealthService();
        _service.Reset();
    }

    [Fact]
    public void CalculateCompositeHealthScore_AppliesExactWeights_Adoption35_Sentiment25_Utilization20_Commercial20()
    {
        // 1. Adoption 100% only -> 35.0
        var adoptionOnly = CustomerHealthService.CalculateCompositeHealthScore(100.0, 0.0, 0.0, 0.0);
        adoptionOnly.Should().Be(35.0);

        // 2. Sentiment 100% only -> 25.0
        var sentimentOnly = CustomerHealthService.CalculateCompositeHealthScore(0.0, 100.0, 0.0, 0.0);
        sentimentOnly.Should().Be(25.0);

        // 3. Utilization 100% only -> 20.0
        var utilizationOnly = CustomerHealthService.CalculateCompositeHealthScore(0.0, 0.0, 100.0, 0.0);
        utilizationOnly.Should().Be(20.0);

        // 4. Commercial 100% only -> 20.0
        var commercialOnly = CustomerHealthService.CalculateCompositeHealthScore(0.0, 0.0, 0.0, 100.0);
        commercialOnly.Should().Be(20.0);

        // 5. Balanced 80.0 across all four dimensions -> 80.0
        var balanced = CustomerHealthService.CalculateCompositeHealthScore(80.0, 80.0, 80.0, 80.0);
        balanced.Should().Be(80.0);

        // 6. Realistic mixed scores: Adoption 92, Sentiment 88, Utilization 94, Commercial 95
        // (92 * 0.35) + (88 * 0.25) + (94 * 0.20) + (95 * 0.20) = 32.2 + 22.0 + 18.8 + 19.0 = 92.0
        var realistic = CustomerHealthService.CalculateCompositeHealthScore(92.0, 88.0, 94.0, 95.0);
        realistic.Should().Be(92.0);

        // 7. Clamping check: inputs > 100 clamped to 100, inputs < 0 clamped to 0
        var clampedUpper = CustomerHealthService.CalculateCompositeHealthScore(150.0, 120.0, 110.0, 105.0);
        clampedUpper.Should().Be(100.0);

        var clampedLower = CustomerHealthService.CalculateCompositeHealthScore(-20.0, -10.0, -5.0, -1.0);
        clampedLower.Should().Be(0.0);
    }

    [Theory]
    [InlineData(100.0, HealthTier.Healthy)]
    [InlineData(85.5, HealthTier.Healthy)]
    [InlineData(75.0, HealthTier.Healthy)]     // Boundary: >= 75 is Healthy
    [InlineData(74.9, HealthTier.AtRisk)]      // Boundary: 50-74.9 is AtRisk
    [InlineData(65.0, HealthTier.AtRisk)]
    [InlineData(50.0, HealthTier.AtRisk)]      // Boundary: 50 is AtRisk
    [InlineData(49.9, HealthTier.Critical)]    // Boundary: < 50 is Critical
    [InlineData(35.0, HealthTier.Critical)]
    [InlineData(0.0, HealthTier.Critical)]
    public void DetermineHealthTier_AccuratelyAssignsTiers_AtBoundaries(double compositeScore, HealthTier expectedTier)
    {
        var tier = CustomerHealthService.DetermineHealthTier(compositeScore);
        tier.Should().Be(expectedTier);
    }

    [Fact]
    public void GetAccounts_ReturnsSeededAccounts_ForDefaultTenant()
    {
        var accounts = _service.GetAccounts(_tenantId);

        accounts.Should().NotBeNull();
        accounts.Should().HaveCount(5);

        // Verifies specific accounts exist and have expected tiers
        accounts.Should().Contain(a => a.AccountId == CustomerHealthService.SeedAccount1Id && a.HealthTier == HealthTier.Healthy);
        accounts.Should().Contain(a => a.AccountId == CustomerHealthService.SeedAccount2Id && a.HealthTier == HealthTier.AtRisk);
        accounts.Should().Contain(a => a.AccountId == CustomerHealthService.SeedAccount3Id && a.HealthTier == HealthTier.Critical);
        accounts.Should().Contain(a => a.AccountId == CustomerHealthService.SeedAccount4Id && a.HealthTier == HealthTier.Healthy);
        accounts.Should().Contain(a => a.AccountId == CustomerHealthService.SeedAccount5Id && a.HealthTier == HealthTier.AtRisk);
    }

    [Fact]
    public void GetAccounts_FiltersByTierAndSearchKeyword()
    {
        // Filter by Critical tier
        var criticalAccounts = _service.GetAccounts(_tenantId, tier: HealthTier.Critical);
        criticalAccounts.Should().HaveCount(1);
        criticalAccounts.First().AccountName.Should().Be("Dubai Logistics One");

        // Filter by Healthy tier
        var healthyAccounts = _service.GetAccounts(_tenantId, tier: HealthTier.Healthy);
        healthyAccounts.Should().HaveCount(2);

        // Filter by search keyword
        var searchResults = _service.GetAccounts(_tenantId, search: "Aramco");
        searchResults.Should().HaveCount(1);
        searchResults.First().Domain.Should().Be("aramcodigital.sa");

        // Filter by CSM search
        var csmResults = _service.GetAccounts(_tenantId, search: "Omar Al-Farhan");
        csmResults.Should().HaveCount(2);
    }

    [Fact]
    public void GetOverview_CalculatesAggregateKPIsCorrectly()
    {
        var overview = _service.GetOverview(_tenantId);

        overview.Should().NotBeNull();
        overview.TotalAccounts.Should().Be(5);
        overview.HealthyCount.Should().Be(2);
        overview.AtRiskCount.Should().Be(2);
        overview.CriticalCount.Should().Be(1);

        overview.TotalArr.Should().Be(240_000m + 120_000m + 85_000m + 180_000m + 64_000m);
        // At risk or critical: Seed 2 ($120k) + Seed 3 ($85k) + Seed 5 ($64k) = $269,000
        overview.ArrAtRisk.Should().Be(269_000m);
        overview.AverageHealthScore.Should().BeGreaterThan(60.0);
        overview.ActivePlaybookCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DetectEarlyWarningRiskTriggers_DetectsNpsDrop_WhenNegativeOrMajorDecline()
    {
        // Negative NPS trigger
        var negativeNpsTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: -10,
            previousNpsScore: 10,
            openTickets: 1,
            urgentTickets: 0,
            inactiveRatio: 0.1,
            totalSeats: 100,
            activeSeats: 90,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 65.0
        );
        negativeNpsTriggers.Should().Contain(t => t.TriggerType == RiskTriggerType.NpsDrop && t.Severity == RiskSeverity.Critical);

        // >= 20 point NPS drop trigger
        var dropTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 30,
            previousNpsScore: 55, // drop of 25 points
            openTickets: 1,
            urgentTickets: 0,
            inactiveRatio: 0.1,
            totalSeats: 100,
            activeSeats: 90,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 70.0
        );
        dropTriggers.Should().Contain(t => t.TriggerType == RiskTriggerType.NpsDrop && t.Severity == RiskSeverity.High);

        // Minor NPS drop (under 20 points, positive) should NOT trigger NpsDrop
        var minorTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 50,
            previousNpsScore: 55, // drop of only 5 points
            openTickets: 1,
            urgentTickets: 0,
            inactiveRatio: 0.1,
            totalSeats: 100,
            activeSeats: 90,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 80.0
        );
        minorTriggers.Should().NotContain(t => t.TriggerType == RiskTriggerType.NpsDrop);
    }

    [Fact]
    public void DetectEarlyWarningRiskTriggers_DetectsTicketSpikes_AndUrgentEscalations()
    {
        // Open tickets >= 5 -> High or Critical spike
        var spikeTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 40,
            previousNpsScore: 40,
            openTickets: 6,
            urgentTickets: 0,
            inactiveRatio: 0.1,
            totalSeats: 100,
            activeSeats: 90,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 70.0
        );
        spikeTriggers.Should().Contain(t => t.TriggerType == RiskTriggerType.TicketSpike && t.Severity == RiskSeverity.High);

        // Multiple urgent tickets (>= 2) -> Critical spike
        var urgentTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 40,
            previousNpsScore: 40,
            openTickets: 4,
            urgentTickets: 2,
            inactiveRatio: 0.1,
            totalSeats: 100,
            activeSeats: 90,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 60.0
        );
        urgentTriggers.Should().Contain(t => t.TriggerType == RiskTriggerType.TicketSpike && t.Severity == RiskSeverity.Critical);

        // Normal ticket load should not trigger spike
        var normalTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 50,
            previousNpsScore: 50,
            openTickets: 2,
            urgentTickets: 0,
            inactiveRatio: 0.1,
            totalSeats: 100,
            activeSeats: 90,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 80.0
        );
        normalTriggers.Should().NotContain(t => t.TriggerType == RiskTriggerType.TicketSpike);
    }

    [Fact]
    public void DetectEarlyWarningRiskTriggers_DetectsInactiveSeatRatio_AboveThresholds()
    {
        // Critical dormancy (>= 50%)
        var criticalRatioTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 50,
            previousNpsScore: 50,
            openTickets: 1,
            urgentTickets: 0,
            inactiveRatio: 0.55,
            totalSeats: 100,
            activeSeats: 45,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 65.0
        );
        criticalRatioTriggers.Should().Contain(t => t.TriggerType == RiskTriggerType.InactiveSeatRatio && t.Severity == RiskSeverity.Critical);

        // High dormancy (>= 35% but < 50%)
        var highRatioTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 50,
            previousNpsScore: 50,
            openTickets: 1,
            urgentTickets: 0,
            inactiveRatio: 0.40,
            totalSeats: 100,
            activeSeats: 60,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 65.0
        );
        highRatioTriggers.Should().Contain(t => t.TriggerType == RiskTriggerType.InactiveSeatRatio && t.Severity == RiskSeverity.High);

        // Healthy seat ratio (< 35%)
        var healthyRatioTriggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 50,
            previousNpsScore: 50,
            openTickets: 1,
            urgentTickets: 0,
            inactiveRatio: 0.10,
            totalSeats: 100,
            activeSeats: 90,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            compositeScore: 85.0
        );
        healthyRatioTriggers.Should().NotContain(t => t.TriggerType == RiskTriggerType.InactiveSeatRatio);
    }

    [Fact]
    public void DetectEarlyWarningRiskTriggers_DetectsContractRenewalRisk_AndPaymentOverdue()
    {
        var triggers = CustomerHealthService.DetectEarlyWarningRiskTriggers(
            npsScore: 30,
            previousNpsScore: 30,
            openTickets: 1,
            urgentTickets: 0,
            inactiveRatio: 0.15,
            totalSeats: 100,
            activeSeats: 85,
            daysUntilRenewal: 45,       // renewal <= 60 days
            daysOverdueInvoice: 35,     // overdue >= 30 days
            compositeScore: 62.0        // composite < 75
        );

        triggers.Should().Contain(t => t.TriggerType == RiskTriggerType.ContractRenewalRisk);
        triggers.Should().Contain(t => t.TriggerType == RiskTriggerType.PaymentOverdue);
    }

    [Fact]
    public void GeneratePlaybooks_RecommendsExecutiveSponsorCheckin_ForCriticalTierOrHighArrAtRisk()
    {
        var accountId = Guid.NewGuid();
        var emptyTriggers = new List<EarlyWarningRiskTrigger>();

        // Critical tier triggers executive check-in
        var criticalPlaybooks = CustomerHealthService.GeneratePlaybooks(
            accountId: accountId,
            accountName: "Enterprise Client",
            tier: HealthTier.Critical,
            compositeScore: 40.0,
            adoptionScore: 40.0,
            sentimentScore: 40.0,
            commercialScore: 40.0,
            inactiveRatio: 0.2,
            arr: 25_000m,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            urgentTickets: 0,
            triggers: emptyTriggers
        );

        criticalPlaybooks.Should().Contain(p => p.PlaybookType == PlaybookType.ExecutiveSponsorCheckin && p.Priority == PlaybookPriority.Urgent);

        // High ARR ($50k+) in AtRisk tier also triggers executive check-in
        var highArrAtRiskPlaybooks = CustomerHealthService.GeneratePlaybooks(
            accountId: accountId,
            accountName: "High Value Client",
            tier: HealthTier.AtRisk,
            compositeScore: 65.0,
            adoptionScore: 70.0,
            sentimentScore: 65.0,
            commercialScore: 70.0,
            inactiveRatio: 0.2,
            arr: 100_000m,
            daysUntilRenewal: 120,
            daysOverdueInvoice: 0,
            urgentTickets: 0,
            triggers: emptyTriggers
        );

        highArrAtRiskPlaybooks.Should().Contain(p => p.PlaybookType == PlaybookType.ExecutiveSponsorCheckin);
    }

    [Fact]
    public void GeneratePlaybooks_RecommendsAdoptionWorkshop_ForLowAdoptionOrDormantSeats()
    {
        var accountId = Guid.NewGuid();
        var emptyTriggers = new List<EarlyWarningRiskTrigger>();

        // Low adoption score (< 60)
        var lowAdoptionPlaybooks = CustomerHealthService.GeneratePlaybooks(
            accountId: accountId,
            accountName: "Adoption Risk Client",
            tier: HealthTier.AtRisk,
            compositeScore: 68.0,
            adoptionScore: 50.0,
            sentimentScore: 80.0,
            commercialScore: 80.0,
            inactiveRatio: 0.15,
            arr: 30_000m,
            daysUntilRenewal: 150,
            daysOverdueInvoice: 0,
            urgentTickets: 0,
            triggers: emptyTriggers
        );

        lowAdoptionPlaybooks.Should().Contain(p => p.PlaybookType == PlaybookType.AdoptionWorkshop);
    }

    [Fact]
    public void GeneratePlaybooks_RecommendsEmergencyDiscountReview_AndTechnicalSwat()
    {
        var accountId = Guid.NewGuid();
        var triggerList = new List<EarlyWarningRiskTrigger>
        {
            new(Guid.NewGuid(), RiskTriggerType.TicketSpike, RiskSeverity.High, "Ticket spike", 6, 5, DateTime.UtcNow)
        };

        var playbooks = CustomerHealthService.GeneratePlaybooks(
            accountId: accountId,
            accountName: "Escalated Account",
            tier: HealthTier.AtRisk,
            compositeScore: 58.0,
            adoptionScore: 70.0,
            sentimentScore: 70.0,
            commercialScore: 40.0, // Commercial < 50
            inactiveRatio: 0.15,
            arr: 60_000m,
            daysUntilRenewal: 45,   // Renewal <= 60
            daysOverdueInvoice: 30, // Overdue >= 30
            urgentTickets: 1,
            triggers: triggerList
        );

        playbooks.Should().Contain(p => p.PlaybookType == PlaybookType.EmergencyDiscountReview);
        playbooks.Should().Contain(p => p.PlaybookType == PlaybookType.TechnicalSwatTriage);
    }

    [Fact]
    public void RecalculateAccountHealth_UpdatesMetrics_RecalculatesTierAndSnapshots()
    {
        // Account 2 begins in AtRisk tier (composite ~62.3)
        var initial = _service.GetAccountById(_tenantId, CustomerHealthService.SeedAccount2Id);
        initial.Should().NotBeNull();
        initial!.HealthTier.Should().Be(HealthTier.AtRisk);
        var initialSnapshotCount = initial.HistoricalSnapshots.Count;

        // Upgrade its adoption, sentiment, utilization, and commercial metrics
        var updateRequest = new UpdateAccountMetricsRequest(
            TotalSeats: 80,
            ActiveSeats: 76, // 95% utilization
            NpsScore: 60,
            PreviousNpsScore: 60,
            OpenTicketsCount: 0,
            UrgentTicketsCount: 0,
            DaysUntilRenewal: 90,
            DaysOverdueInvoice: 0,
            AdoptionScore: 90.0,
            SentimentScore: 88.0,
            UtilizationScore: 92.0,
            CommercialScore: 95.0
        );

        var updated = _service.RecalculateAccountHealth(_tenantId, CustomerHealthService.SeedAccount2Id, updateRequest);

        updated.Should().NotBeNull();
        updated!.CompositeHealthScore.Should().BeGreaterThanOrEqualTo(75.0);
        updated.HealthTier.Should().Be(HealthTier.Healthy);
        updated.PredictedChurnProbability.Should().BeLessThan(initial.PredictedChurnProbability);
        updated.HistoricalSnapshots.Count.Should().Be(initialSnapshotCount + 1);
        updated.HistoricalSnapshots.Last().CompositeHealthScore.Should().Be(updated.CompositeHealthScore);
        updated.HistoricalSnapshots.Last().HealthTier.Should().Be(HealthTier.Healthy);
    }

    [Fact]
    public void UpdatePlaybookStatus_UpdatesStatusAndCompletedTimestamp()
    {
        var account = _service.GetAccountById(_tenantId, CustomerHealthService.SeedAccount2Id);
        account.Should().NotBeNull();
        account!.ActivePlaybooks.Should().NotBeEmpty();

        var playbook = account.ActivePlaybooks.First();
        var playbookId = playbook.PlaybookId;

        // Transition from Suggested to Active
        var activeResult = _service.UpdatePlaybookStatus(_tenantId, playbookId, PlaybookStatus.Active, "CSM kicked off workshop with CTO");
        activeResult.Should().NotBeNull();
        activeResult!.Status.Should().Be(PlaybookStatus.Active);
        activeResult.Notes.Should().Be("CSM kicked off workshop with CTO");
        activeResult.CompletedAtUtc.Should().BeNull();

        // Complete the playbook
        var completedResult = _service.UpdatePlaybookStatus(_tenantId, playbookId, PlaybookStatus.Completed, "Customer signed 2-year expansion");
        completedResult.Should().NotBeNull();
        completedResult!.Status.Should().Be(PlaybookStatus.Completed);
        completedResult.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void CreateAccount_ValidatesInput_AndCalculatesInitialRadarProfile()
    {
        var request = new CreateCustomerAccountRequest(
            TenantId: _tenantId,
            AccountName: "Neom Autonomous Tech",
            Domain: "neomtech.sa",
            Industry: "Smart Cities & AI",
            AssignedCsm: "Hala Al-Mansoor",
            AnnualRecurringRevenue: 350_000m,
            TotalSeats: 200,
            ActiveSeats: 190,
            NpsScore: 75,
            PreviousNpsScore: 70,
            OpenTicketsCount: 0,
            UrgentTicketsCount: 0,
            DaysUntilRenewal: 300,
            DaysOverdueInvoice: 0,
            AdoptionScore: 94.0,
            SentimentScore: 92.0,
            UtilizationScore: 95.0,
            CommercialScore: 98.0,
            Tags: new List<string> { "Strategic", "HyperGrowth" }
        );

        var created = _service.CreateAccount(request);

        created.Should().NotBeNull();
        created.AccountId.Should().NotBeEmpty();
        created.AccountName.Should().Be("Neom Autonomous Tech");
        created.HealthTier.Should().Be(HealthTier.Healthy);
        created.CompositeHealthScore.Should().BeGreaterThan(90.0);
        created.InactiveSeatRatio.Should().Be(0.05);
        created.RiskTriggers.Should().BeEmpty();
        created.HistoricalSnapshots.Should().HaveCount(1);

        // Fetch back by ID
        var fetched = _service.GetAccountById(_tenantId, created.AccountId);
        fetched.Should().NotBeNull();
        fetched!.AccountId.Should().Be(created.AccountId);
    }
}
