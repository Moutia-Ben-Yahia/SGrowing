using SGrowing.Api.Modules.CRM;
using Xunit;

namespace SGrowing.Tests.CRM;

public class LeadEnrichmentRadarServiceTests
{
    private readonly LeadEnrichmentRadarService _service;

    public LeadEnrichmentRadarServiceTests()
    {
        _service = new LeadEnrichmentRadarService();
    }

    #region Disposable Email Detection Tests

    [Theory]
    [InlineData("test@mailinator.com")]
    [InlineData("random.user@tempmail.com")]
    [InlineData("burn123@guerrillamail.com")]
    [InlineData("fake@10minutemail.com")]
    [InlineData("discard@throwawaymail.com")]
    [InlineData("trash@yopmail.com")]
    [InlineData("burner@burnermail.io")]
    public async Task DisposableEmailDetection_KnownBurnerDomains_FlagsAsDisposableAndLowDeliverability(string email)
    {
        var result = await _service.VerifyEmailAsync(email);

        Assert.True(result.Details.IsDisposable);
        Assert.Equal(DeliverabilityStatus.Disposable, result.Details.Status);
        Assert.True(result.Details.DeliverabilityScore <= 10);
        Assert.NotNull(result.Details.DisposableDomainMatched);
    }

    [Fact]
    public async Task DisposableEmailDetection_CaseInsensitiveAndSubdomains_CorrectlyFlagsDisposable()
    {
        var result = await _service.VerifyEmailAsync("  ALERT@SubDomain.MailInator.COM  ");

        Assert.True(result.Details.IsDisposable);
        Assert.Equal(DeliverabilityStatus.Disposable, result.Details.Status);
        Assert.Equal("mailinator.com", result.Details.DisposableDomainMatched);
    }

    [Fact]
    public void DisposableEmailBlacklist_ContainsOver50KnownProviders()
    {
        Assert.True(LeadEnrichmentRadarService.DisposableDomainsBlacklist.Count >= 50);
        Assert.Contains("mailinator.com", LeadEnrichmentRadarService.DisposableDomainsBlacklist);
        Assert.Contains("trashmail.com", LeadEnrichmentRadarService.DisposableDomainsBlacklist);
        Assert.Contains("tempmailo.com", LeadEnrichmentRadarService.DisposableDomainsBlacklist);
    }

    #endregion

    #region DNS & Deliverability Verification Tests

    [Fact]
    public async Task DnsVerification_ValidCorporateDomain_ReturnsAuthenticatedMxSpfDkimDmarc()
    {
        var result = await _service.VerifyEmailAsync("cto@stripe.com");

        Assert.False(result.Details.IsDisposable);
        Assert.Equal(DeliverabilityStatus.Valid, result.Details.Status);
        Assert.True(result.Details.HasValidMxRecord);
        Assert.NotEmpty(result.Details.MxRecords);
        Assert.True(result.Details.SpfRecordFound);
        Assert.True(result.Details.DkimValid);
        Assert.True(result.Details.DmarcEnforced);
        Assert.True(result.Details.DeliverabilityScore >= 90);
    }

    [Theory]
    [InlineData("corrupted-email-without-at.com")]
    [InlineData("user@nonexistent-server-xyz.invalid")]
    [InlineData("user@test-domain.test")]
    public async Task DnsVerification_InvalidHostOrSyntax_ReturnsUndeliverable(string email)
    {
        var result = await _service.VerifyEmailAsync(email);

        Assert.Equal(DeliverabilityStatus.Undeliverable, result.Details.Status);
        Assert.False(result.Details.HasValidMxRecord);
        Assert.True(result.Details.DeliverabilityScore <= 15);
    }

    [Fact]
    public async Task DnsVerification_FreemailProvider_ReturnsRiskyDeliverabilityStatus()
    {
        var result = await _service.VerifyEmailAsync("founder@gmail.com");

        Assert.False(result.Details.IsDisposable);
        Assert.Equal(DeliverabilityStatus.Risky, result.Details.Status);
        Assert.True(result.Details.HasValidMxRecord);
        Assert.Equal(60, result.Details.DeliverabilityScore);
        Assert.Contains("Public webmail", result.Details.Reason);
    }

    #endregion

    #region Email Parsing & Multi-Part TLD Tests

    [Fact]
    public void EmailParsing_SubdomainsAndMultiPartTlds_CorrectlyExtractsRootDomain()
    {
        var (cleanEmail, domain, rootDomain, isValid) =
            LeadEnrichmentRadarService.ParseEmailAndDomain("sales@corp.logisticsflow.co.uk");

        Assert.True(isValid);
        Assert.Equal("sales@corp.logisticsflow.co.uk", cleanEmail);
        Assert.Equal("corp.logisticsflow.co.uk", domain);
        Assert.Equal("logisticsflow.co.uk", rootDomain);
    }

    [Fact]
    public void EmailParsing_PlusAddressingAndWhitespace_TrimsAndNormalizes()
    {
        var (cleanEmail, domain, rootDomain, isValid) =
            LeadEnrichmentRadarService.ParseEmailAndDomain("   Elena.Rostova+CampaignA@FinTechGroup.COM   ");

        Assert.True(isValid);
        Assert.Equal("elena.rostova+campaigna@fintechgroup.com", cleanEmail);
        Assert.Equal("fintechgroup.com", domain);
        Assert.Equal("fintechgroup.com", rootDomain);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-symbol.com")]
    [InlineData("user@")]
    [InlineData("@domain.com")]
    [InlineData("user@domain with spaces.com")]
    public void EmailParsing_InvalidFormats_ReturnsSyntaxError(string input)
    {
        var (_, _, _, isValid) = LeadEnrichmentRadarService.ParseEmailAndDomain(input);
        Assert.False(isValid);
    }

    #endregion

    #region Firmographic & Technographic Synthesis Tests

    [Theory]
    [InlineData("1-10", "<$1M")]
    [InlineData("11-50", "$1M-$5M")]
    [InlineData("51-200", "$5M-$20M")]
    [InlineData("201-1000", "$20M-$100M")]
    [InlineData("1000+", "$100M+")]
    public void FirmographicSynthesis_HeadcountTiers_MapToExpectedAnnualRevenue(string tier, string expectedRevenue)
    {
        var firmo = LeadEnrichmentRadarService.SynthesizeFirmographics(
            domain: "samplecorp.com",
            rootDomain: "samplecorp.com",
            companyNameHint: "Sample Corp",
            industryHint: "B2B SaaS",
            headcountHint: tier
        );

        Assert.Equal(tier, firmo.HeadcountTier);
        Assert.Equal(expectedRevenue, firmo.EstimatedAnnualRevenue);
    }

    [Theory]
    [InlineData("payfintech.io", "Fintech")]
    [InlineData("shipfreightfleet.com", "Logistics")]
    [InlineData("healthclinicbiomed.org", "Healthtech")]
    [InlineData("myshopcartstore.com", "E-commerce")]
    [InlineData("retailapparelbrand.com", "Retail")]
    public void FirmographicSynthesis_DomainKeywordDetection_IdentifiesIndustryVerticals(string domain, string expectedIndustry)
    {
        var firmo = LeadEnrichmentRadarService.SynthesizeFirmographics(
            domain: domain,
            rootDomain: domain,
            companyNameHint: null,
            industryHint: null,
            headcountHint: "51-200"
        );

        Assert.Equal(expectedIndustry, firmo.Industry);
    }

    [Fact]
    public void TechnographicStackDetection_EnterpriseScale_DetectsCrmCloudFrontendAnalytics()
    {
        var techno = LeadEnrichmentRadarService.SynthesizeTechnographics("enterprisefin.com", "Fintech", "1000+");

        Assert.True(techno.TechnographicScore >= 80);
        Assert.Contains(techno.Technologies, t => t.Category == "CRM");
        Assert.Contains(techno.Technologies, t => t.Category == "Cloud");
        Assert.Contains(techno.Technologies, t => t.Category == "Analytics");
        Assert.Contains(techno.Technologies, t => t.Category == "Frontend");
    }

    #endregion

    #region Composite Score Formula & Weighting Boundaries

    [Fact]
    public void CompositeScoreFormula_ExactWeightingsAndFloor_ComputesCorrectly()
    {
        // Formula: LeadScore = min(100, floor(0.35 * S_firmo + 0.25 * S_techno + 0.25 * S_intent + 0.15 * S_deliv))
        int firmo = 80;
        int techno = 70;
        int intent = 90;
        int deliv = 100;

        // 0.35 * 80 = 28.0
        // 0.25 * 70 = 17.5
        // 0.25 * 90 = 22.5
        // 0.15 * 100 = 15.0
        // Sum = 28 + 17.5 + 22.5 + 15 = 83.0
        var breakdown = LeadEnrichmentRadarService.ComputeScoreBreakdown(firmo, techno, intent, deliv);

        Assert.Equal(0.35, breakdown.FirmographicWeight);
        Assert.Equal(0.25, breakdown.TechnographicWeight);
        Assert.Equal(0.25, breakdown.IntentWeight);
        Assert.Equal(0.15, breakdown.DeliverabilityWeight);
        Assert.Equal(83, breakdown.CompositeScore);
    }

    [Fact]
    public void CompositeScoreFormula_MaxScoreClamping_DoesNotExceed100()
    {
        var breakdown = LeadEnrichmentRadarService.ComputeScoreBreakdown(100, 100, 100, 100);
        Assert.Equal(100, breakdown.CompositeScore);
    }

    [Fact]
    public void CompositeScoreFormula_ZeroScores_YieldsZero()
    {
        var breakdown = LeadEnrichmentRadarService.ComputeScoreBreakdown(0, 0, 0, 0);
        Assert.Equal(0, breakdown.CompositeScore);
    }

    #endregion

    #region Qualification Gating Threshold Tests

    [Theory]
    [InlineData(100, QualificationStatus.SalesQualifiedLead)]
    [InlineData(85, QualificationStatus.SalesQualifiedLead)]
    [InlineData(75, QualificationStatus.SalesQualifiedLead)]
    [InlineData(74, QualificationStatus.MarketingQualifiedLead)]
    [InlineData(60, QualificationStatus.MarketingQualifiedLead)]
    [InlineData(50, QualificationStatus.MarketingQualifiedLead)]
    [InlineData(49, QualificationStatus.Unqualified)]
    [InlineData(25, QualificationStatus.Unqualified)]
    [InlineData(0, QualificationStatus.Unqualified)]
    public void QualificationGating_ScoreThresholds_ClassifyAccurately(int score, QualificationStatus expectedStatus)
    {
        var status = LeadEnrichmentRadarService.DetermineQualification(score);
        Assert.Equal(expectedStatus, status);
    }

    #endregion

    #region Round-Robin Sales Rep Routing Tests

    [Fact]
    public async Task RoundRobinRouting_SqlLead_AssignsSalesRepresentative()
    {
        var req = new EnrichLeadRequest(
            Email: "vp.engineering@highgrowthcloud.io",
            CompanyName: "HighGrowth Cloud",
            IntentScore: 95,
            IndustryHint: "B2B SaaS",
            HeadcountHint: "1000+"
        );

        var lead = await _service.EnrichLeadAsync(req);

        Assert.Equal(QualificationStatus.SalesQualifiedLead, lead.QualificationStatus);
        Assert.NotNull(lead.AssignedSalesRep);
        Assert.Contains(lead.AssignedSalesRep, LeadEnrichmentRadarService.SalesRepPool);
    }

    [Fact]
    public async Task RoundRobinRouting_MqlAndUnqualified_LeaveSalesRepUnassigned()
    {
        var mqlReq = new EnrichLeadRequest(
            Email: "mid@retailgrowthhub.com",
            CompanyName: "Retail Growth Hub",
            IntentScore: 55,
            IndustryHint: "Retail",
            HeadcountHint: "11-50"
        );

        var mqlLead = await _service.EnrichLeadAsync(mqlReq);

        Assert.NotEqual(QualificationStatus.SalesQualifiedLead, mqlLead.QualificationStatus);
        Assert.Null(mqlLead.AssignedSalesRep);
    }

    [Fact]
    public async Task RoundRobinRouting_MultipleSqlLeads_DistributesEvenlyAcrossAePool()
    {
        var assignedReps = new List<string>();

        for (int i = 0; i < LeadEnrichmentRadarService.SalesRepPool.Length * 2; i++)
        {
            var req = new EnrichLeadRequest(
                Email: $"director{i}@enterprise-fintech-{i}.com",
                CompanyName: $"Enterprise Fintech {i}",
                IntentScore: 90,
                IndustryHint: "Fintech",
                HeadcountHint: "1000+"
            );

            var lead = await _service.EnrichLeadAsync(req);
            Assert.Equal(QualificationStatus.SalesQualifiedLead, lead.QualificationStatus);
            Assert.NotNull(lead.AssignedSalesRep);
            assignedReps.Add(lead.AssignedSalesRep!);
        }

        // Each sales rep in pool should have received leads
        foreach (var rep in LeadEnrichmentRadarService.SalesRepPool)
        {
            Assert.Contains(rep, assignedReps);
        }
    }

    #endregion

    #region Concurrent Operations & Thread Safety

    [Fact]
    public async Task ConcurrentEnrichment_MultiThreadedRequests_MaintainsThreadSafety()
    {
        const int concurrentCount = 20;
        var tasks = Enumerable.Range(1, concurrentCount).Select(i =>
        {
            var req = new EnrichLeadRequest(
                Email: $"concurrent.lead{i}@scaleplatform{i}.io",
                CompanyName: $"Scale Platform {i}",
                IntentScore: 70 + (i % 25)
            );
            return _service.EnrichLeadAsync(req);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(concurrentCount, results.Length);
        Assert.Equal(concurrentCount, results.Select(r => r.Id).Distinct().Count());

        var allLeads = await _service.GetAllLeadsAsync();
        Assert.True(allLeads.Count >= concurrentCount);
    }

    #endregion

    #region Seed Data & Batch Operations Tests

    [Fact]
    public async Task SeedData_ContainsFiveDiverseLeadsAcrossTiers()
    {
        var leads = await _service.GetAllLeadsAsync();

        Assert.True(leads.Count >= 5);
        Assert.Contains(leads, l => l.QualificationStatus == QualificationStatus.SalesQualifiedLead);
        Assert.Contains(leads, l => l.QualificationStatus == QualificationStatus.MarketingQualifiedLead);
        Assert.Contains(leads, l => l.QualificationStatus == QualificationStatus.Unqualified);
        Assert.Contains(leads, l => l.DnsVerification.Status == DeliverabilityStatus.Disposable);
    }

    [Fact]
    public async Task BatchRecalculation_RecalculatesAllLeadsAndAggregatesMetrics()
    {
        var result = await _service.RecalculateAllScoresAsync();

        Assert.True(result.TotalProcessed >= 5);
        Assert.Equal(result.TotalProcessed, result.SqlCount + result.MqlCount + result.UnqualifiedCount);
        Assert.True(result.AverageScore > 0);
    }

    [Fact]
    public async Task GetKpiMetrics_ComputesAccuratePercentagesAndCounts()
    {
        var metrics = await _service.GetKpiMetricsAsync();

        Assert.True(metrics.TotalLeads >= 5);
        Assert.True(metrics.SqlRatePercent >= 0 && metrics.SqlRatePercent <= 100);
        Assert.True(metrics.DeliverabilityPassRatePercent >= 0 && metrics.DeliverabilityPassRatePercent <= 100);
        Assert.True(metrics.AverageLeadScore >= 0 && metrics.AverageLeadScore <= 100);
        Assert.True(metrics.DisposableCount >= 1);
    }

    [Fact]
    public async Task DeleteLead_ExistingLead_RemovesSuccessfully()
    {
        var req = new EnrichLeadRequest("temporary.test@disposable-lead-test.com");
        var created = await _service.EnrichLeadAsync(req);

        bool deleted = await _service.DeleteLeadAsync(created.Id);
        Assert.True(deleted);

        var retrieved = await _service.GetLeadByIdAsync(created.Id);
        Assert.Null(retrieved);
    }

    #endregion
}
