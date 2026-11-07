using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.CRM.IntentScoring;
using Xunit;

namespace SGrowing.Tests.Unit;

public class IntentScoringTests
{
    private readonly IntentScoringService _service;
    private readonly Guid _tenantId = IntentScoringService.DefaultTenantId;

    public IntentScoringTests()
    {
        _service = new IntentScoringService();
        _service.Reset(_tenantId);
    }

    [Theory]
    [InlineData(100.0, IcpGrade.GradeA)]
    [InlineData(95.0, IcpGrade.GradeA)]
    [InlineData(90.0, IcpGrade.GradeA)]
    [InlineData(89.9, IcpGrade.GradeB)]
    [InlineData(80.0, IcpGrade.GradeB)]
    [InlineData(75.0, IcpGrade.GradeB)]
    [InlineData(74.9, IcpGrade.GradeC)]
    [InlineData(60.0, IcpGrade.GradeC)]
    [InlineData(50.0, IcpGrade.GradeC)]
    [InlineData(49.9, IcpGrade.GradeD)]
    [InlineData(25.0, IcpGrade.GradeD)]
    [InlineData(0.0, IcpGrade.GradeD)]
    public void ComputeIcpGrade_AssignsCorrectGrade_AcrossFitScoreSpectrum(double fitScore, IcpGrade expectedGrade)
    {
        var grade = _service.ComputeIcpGrade(fitScore);
        grade.Should().Be(expectedGrade);
    }

    [Fact]
    public void CalculateDecayFactor_ComputesAccurateExponentialDecay_ForConfiguredHalfLife()
    {
        var now = DateTime.UtcNow;
        const int halfLifeDays = 14;

        // Exactly at occurrence time: decay factor is 1.0 (no decay)
        var factor0 = _service.CalculateDecayFactor(now, halfLifeDays, now);
        factor0.Should().BeApproximately(1.0, 0.0001);

        // Exactly 1 half-life (14 days): decay factor is 0.5
        var factor14 = _service.CalculateDecayFactor(now.AddDays(-14), halfLifeDays, now);
        factor14.Should().BeApproximately(0.5, 0.0001);

        // Exactly 2 half-lives (28 days): decay factor is 0.25
        var factor28 = _service.CalculateDecayFactor(now.AddDays(-28), halfLifeDays, now);
        factor28.Should().BeApproximately(0.25, 0.0001);

        // Exactly 3 half-lives (42 days): decay factor is 0.125
        var factor42 = _service.CalculateDecayFactor(now.AddDays(-42), halfLifeDays, now);
        factor42.Should().BeApproximately(0.125, 0.0001);

        // Negative elapsed time (future date): clamped to 1.0
        var factorFuture = _service.CalculateDecayFactor(now.AddDays(5), halfLifeDays, now);
        factorFuture.Should().Be(1.0);

        // Half life <= 0: defaults safely to 1.0
        var factorInvalid = _service.CalculateDecayFactor(now.AddDays(-10), 0, now);
        factorInvalid.Should().Be(1.0);
    }

    [Fact]
    public void GetLeads_ReturnsAllSeededLeads_OrderedByCompositeScoreDescending()
    {
        var leads = _service.GetLeads(_tenantId);

        leads.Should().NotBeEmpty();
        leads.Count.Should().Be(6);

        // Verify descending order
        for (int i = 0; i < leads.Count - 1; i++)
        {
            leads[i].CompositeScore.Should().BeGreaterThanOrEqualTo(leads[i + 1].CompositeScore);
        }

        // Aramco or Al-Rajhi should be at the top with high scores
        leads[0].IcpGrade.Should().BeOneOf(IcpGrade.GradeA, IcpGrade.GradeB);
        leads[0].CompositeScore.Should().BeGreaterThan(75.0);
    }

    [Fact]
    public void GetLeadById_ReturnsSpecificLead_WithDetailedScoreBreakdown()
    {
        var lead = _service.GetLeadById(IntentScoringService.AramcoLeadId, _tenantId);

        lead.Should().NotBeNull();
        lead!.ContactEmail.Should().Be("omar.otaibi@aramcodigital.com");
        lead.Firmographics.CompanyName.Should().Be("Aramco Digital Ventures");
        lead.IcpGrade.Should().Be(IcpGrade.GradeA);

        var breakdown = lead.ScoreBreakdown;
        breakdown.Should().NotBeNull();
        breakdown.FirmographicScore.Should().Be(96.0);
        breakdown.FirmographicWeight.Should().Be(0.25);
        breakdown.FirmographicWeighted.Should().Be(24.0);
        breakdown.CompositeScore.Should().Be(lead.CompositeScore);
    }

    [Fact]
    public void IngestSignal_FirstPartySignal_RecalculatesLeadScoreAndDecayedBuckets()
    {
        var targetLeadId = IntentScoringService.MaghrebLeadId;
        var initial = _service.GetLeadById(targetLeadId, _tenantId)!;
        var initialScore = initial.CompositeScore;
        var initialSignalCount = initial.ActiveSignals.Count;

        var request = new IngestSignalRequest(
            targetLeadId,
            IntentSignalType.DemoRequest,
            SignalSource.FirstParty,
            "/book-enterprise-demo",
            RawScore: 50.0,
            Weight: 1.5,
            OccurredAtUtc: DateTime.UtcNow,
            Metadata: new Dictionary<string, string> { { "channel", "web_hero" } },
            TenantId: _tenantId
        );

        var updated = _service.IngestSignal(request);

        updated.ActiveSignals.Count.Should().Be(initialSignalCount + 1);
        updated.CompositeScore.Should().BeGreaterThan(initialScore);
        updated.ScoreBreakdown.FirstPartyBehavioralDecayedScore.Should().BeGreaterThan(initial.ScoreBreakdown.FirstPartyBehavioralDecayedScore);
    }

    [Fact]
    public void IngestSignal_ThirdPartySurge_UpdatesSurgeScoreAndTriggersStageTransition()
    {
        var targetLeadId = IntentScoringService.FinTechUkLeadId;
        var initial = _service.GetLeadById(targetLeadId, _tenantId)!;

        var request = new IngestSignalRequest(
            targetLeadId,
            IntentSignalType.BomboraSurgeTopic,
            SignalSource.ThirdPartySurge,
            "Enterprise CPQ & Billing Engine",
            RawScore: 50.0,
            Weight: 1.5,
            OccurredAtUtc: DateTime.UtcNow,
            Metadata: null,
            TenantId: _tenantId
        );

        var updated = _service.IngestSignal(request);

        updated.ScoreBreakdown.ThirdPartySurgeDecayedScore.Should().BeGreaterThan(initial.ScoreBreakdown.ThirdPartySurgeDecayedScore);
        updated.CompositeScore.Should().BeGreaterThan(initial.CompositeScore);
    }

    [Fact]
    public void AutoTransition_PromotesToSql_WhenScoreExceedsSqlThresholdAndIcpIsGradeAOrB()
    {
        // Al-Rajhi starts with Grade A and high intent
        var lead = _service.GetLeadById(IntentScoringService.AlRajhiLeadId, _tenantId)!;
        lead.IcpGrade.Should().Be(IcpGrade.GradeA);
        lead.CompositeScore.Should().BeGreaterThanOrEqualTo(80.0);
        lead.QualificationStage.Should().Be(LeadQualificationStage.SalesQualifiedLead);
        lead.BuyingStage.Should().BeOneOf(PredictiveBuyingStage.HighIntent, PredictiveBuyingStage.ReadyToBuy);
        lead.RecommendedAction.Should().BeOneOf(NextActionRecommendation.ScheduleExecutiveBriefing, NextActionRecommendation.RouteToSeniorSdrImmediate);

        // Aramco also qualifies as SQL with ReadyToBuy stage
        var aramco = _service.GetLeadById(IntentScoringService.AramcoLeadId, _tenantId)!;
        aramco.IcpGrade.Should().Be(IcpGrade.GradeA);
        aramco.CompositeScore.Should().BeGreaterThanOrEqualTo(80.0);
        aramco.QualificationStage.Should().Be(LeadQualificationStage.SalesQualifiedLead);
    }

    [Fact]
    public void AutoTransition_PreventsSqlTransition_WhenIcpGradeIsCOrD_EvenIfScoreHigh()
    {
        // Create a lead with Grade C fit (FitScore = 65)
        var leadId = Guid.NewGuid();
        var createRequest = new CreateOrUpdateLeadIntentRequest(
            leadId,
            _tenantId,
            "test.cgrade@example.com",
            "Tariq Test",
            "Software Developer",
            new FirmographicProfile("example.com", "Example C-Grade Co", "Education", "11-50", "$1M-$5M", "Global", 2020, 65.0, IcpGrade.GradeC),
            new TechnographicProfile(["PHP", "MySQL"], ["Legacy"], false, 60.0)
        );

        _service.CreateOrUpdateLead(createRequest);

        // Ingest massive fresh signals so raw intent hits max 100
        _service.IngestSignal(new IngestSignalRequest(leadId, IntentSignalType.DemoRequest, SignalSource.FirstParty, "/demo", 100.0, 2.0, DateTime.UtcNow, null, _tenantId));
        _service.IngestSignal(new IngestSignalRequest(leadId, IntentSignalType.PricingPageView, SignalSource.FirstParty, "/pricing", 100.0, 2.0, DateTime.UtcNow, null, _tenantId));
        _service.IngestSignal(new IngestSignalRequest(leadId, IntentSignalType.BomboraSurgeTopic, SignalSource.ThirdPartySurge, "Surge Topic", 100.0, 2.0, DateTime.UtcNow, null, _tenantId));

        var evaluated = _service.GetLeadById(leadId, _tenantId)!;

        // Even though score exceeds SQL threshold (80.0), Grade C prevents SQL qualification!
        evaluated.CompositeScore.Should().BeGreaterThanOrEqualTo(80.0);
        evaluated.IcpGrade.Should().Be(IcpGrade.GradeC);
        evaluated.QualificationStage.Should().Be(LeadQualificationStage.MarketingQualifiedLead);
    }

    [Fact]
    public void AutoTransition_SetsMarketingQualifiedLead_WhenScoreBetweenMqlAndSqlThresholds()
    {
        var lead = _service.GetLeadById(IntentScoringService.MaghrebLeadId, _tenantId)!;
        var config = _service.GetConfig(_tenantId);

        lead.CompositeScore.Should().BeGreaterThanOrEqualTo(config.MqlThresholdScore);
        if (lead.CompositeScore < config.SqlThresholdScore)
        {
            lead.QualificationStage.Should().Be(LeadQualificationStage.MarketingQualifiedLead);
        }
    }

    [Fact]
    public void AutoTransition_KeepsUnqualified_WhenScoreBelowMqlThreshold()
    {
        var lead = _service.GetLeadById(IntentScoringService.LegacyHardwareLeadId, _tenantId)!;
        var config = _service.GetConfig(_tenantId);

        lead.CompositeScore.Should().BeLessThan(config.MqlThresholdScore);
        lead.QualificationStage.Should().Be(LeadQualificationStage.Unqualified);
        lead.IcpGrade.Should().Be(IcpGrade.GradeD);
        lead.RecommendedAction.Should().Be(NextActionRecommendation.DisqualifyOrSuppress);
    }

    [Fact]
    public void UpdateConfig_ModifiesWeightsAndTriggersAutoRecalculation()
    {
        var initialOverview = _service.GetOverview(_tenantId);

        var updateDto = new UpdateIntentConfigDto(
            WeightFirmographic: 0.40,
            WeightTechnographic: 0.20,
            WeightFirstPartyBehavioral: 0.20,
            WeightThirdPartySurge: 0.20,
            DecayHalfLifeDays: 7,
            MqlThresholdScore: 50.0,
            SqlThresholdScore: 75.0,
            TargetIndustries: ["Fintech", "B2B SaaS"],
            TargetEmployeeTiers: ["1000+"],
            TargetRegions: ["MENA - Riyadh"]
        );

        var updatedConfig = _service.UpdateConfig(_tenantId, updateDto);

        updatedConfig.WeightFirmographic.Should().Be(0.40);
        updatedConfig.DecayHalfLifeDays.Should().Be(7);
        updatedConfig.MqlThresholdScore.Should().Be(50.0);
        updatedConfig.SqlThresholdScore.Should().Be(75.0);

        // Verification that recalculation executed automatically
        var newOverview = _service.GetOverview(_tenantId);
        newOverview.ActiveConfig.MqlThresholdScore.Should().Be(50.0);
        newOverview.ComputedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BatchRecalculate_RecomputesAllTenantLeadsAndReturnsAggregates()
    {
        var response = _service.BatchRecalculate(_tenantId);

        response.ProcessedCount.Should().Be(6);
        response.SqlCount.Should().BeGreaterThanOrEqualTo(1);
        response.AverageScore.Should().BeGreaterThan(0.0);
        response.RecalculatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetOverview_AggregatesAccurateIcpDistributionAndTopSurgingAccounts()
    {
        var overview = _service.GetOverview(_tenantId);

        overview.TotalLeads.Should().Be(6);
        (overview.GradeACount + overview.GradeBCount + overview.GradeCCount + overview.GradeDCount).Should().Be(overview.TotalLeads);
        (overview.SqlCount + overview.MqlCount + overview.UnqualifiedCount).Should().Be(overview.TotalLeads);
        overview.TopSurgingLeads.Count.Should().BeLessThanOrEqualTo(5);
        overview.TopSurgingLeads.First().CompositeScore.Should().BeGreaterThanOrEqualTo(overview.TopSurgingLeads.Last().CompositeScore);
    }

    [Fact]
    public void IngestSignal_ThrowsKeyNotFoundException_WhenLeadDoesNotExist()
    {
        var invalidLeadId = Guid.NewGuid();
        var request = new IngestSignalRequest(
            invalidLeadId,
            IntentSignalType.PricingPageView,
            SignalSource.FirstParty,
            "/pricing",
            30.0,
            1.0,
            DateTime.UtcNow,
            null,
            _tenantId
        );

        Action act = () => _service.IngestSignal(request);

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage($"*{invalidLeadId}*");
    }

    [Fact]
    public void Reset_RestoresInitialSeededLeadsAndConfig()
    {
        // Mutate by adding a lead
        var customLeadId = Guid.NewGuid();
        _service.CreateOrUpdateLead(new CreateOrUpdateLeadIntentRequest(
            customLeadId,
            _tenantId,
            "temp@test.com",
            "Temp Name",
            "Analyst",
            new FirmographicProfile("test.com", "Temp Corp", "Fintech", "51-200", "$10M-$50M", "MENA - Dubai", 2022, 90.0, IcpGrade.GradeA),
            new TechnographicProfile(["AWS"], ["Cloud"], false, 80.0)
        ));

        _service.GetLeads(_tenantId).Count.Should().Be(7);

        // Reset
        _service.Reset(_tenantId);

        var restored = _service.GetLeads(_tenantId);
        restored.Count.Should().Be(6);
        _service.GetLeadById(customLeadId, _tenantId).Should().BeNull();
    }
}
