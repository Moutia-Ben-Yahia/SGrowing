using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.CRM.IntentScoring;

/// <summary>
/// Service interface for Predictive Buying Intent & Multi-Signal Lead Scoring.
/// </summary>
public interface IIntentScoringService
{
    IReadOnlyList<LeadIntentRecord> GetLeads(Guid tenantId);
    LeadIntentRecord? GetLeadById(Guid leadId, Guid tenantId);
    LeadIntentRecord IngestSignal(IngestSignalRequest request);
    LeadIntentRecord CreateOrUpdateLead(CreateOrUpdateLeadIntentRequest request);
    LeadIntentRecord EvaluateLead(Guid leadId, Guid tenantId);
    BatchRecalculateResponse BatchRecalculate(Guid tenantId);
    IntentScoringConfig GetConfig(Guid tenantId);
    IntentScoringConfig UpdateConfig(Guid tenantId, UpdateIntentConfigDto update);
    IntentScoringOverviewDto GetOverview(Guid tenantId);
    double CalculateDecayFactor(DateTime occurredAtUtc, int halfLifeDays, DateTime nowUtc);
    IcpGrade ComputeIcpGrade(double fitScore);
    void Reset(Guid tenantId);
}

/// <summary>
/// Thread-safe in-memory implementation of the Predictive Buying Intent & Multi-Signal Lead Scoring Engine.
/// </summary>
public class IntentScoringService : IIntentScoringService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Known seeded lead IDs for deterministic tests
    public static readonly Guid AramcoLeadId = Guid.Parse("a1111111-aaaa-1111-aaaa-111111111111");
    public static readonly Guid MaghrebLeadId = Guid.Parse("b2222222-bbbb-2222-bbbb-222222222222");
    public static readonly Guid AlRajhiLeadId = Guid.Parse("c3333333-cccc-3333-cccc-333333333333");
    public static readonly Guid FinTechUkLeadId = Guid.Parse("d4444444-dddd-4444-dddd-444444444444");
    public static readonly Guid DubaiRetailLeadId = Guid.Parse("e5555555-eeee-5555-eeee-555555555555");
    public static readonly Guid LegacyHardwareLeadId = Guid.Parse("f6666666-ffff-6666-ffff-666666666666");

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, LeadIntentRecord>> _leadsByTenant = new();
    private readonly ConcurrentDictionary<Guid, IntentScoringConfig> _configsByTenant = new();
    private readonly object _syncLock = new();

    public IntentScoringService()
    {
        SeedDefaultTenant(DefaultTenantId);
    }

    public void Reset(Guid tenantId)
    {
        lock (_syncLock)
        {
            _leadsByTenant.TryRemove(tenantId, out _);
            _configsByTenant.TryRemove(tenantId, out _);
            SeedDefaultTenant(tenantId);
        }
    }

    public IntentScoringConfig GetConfig(Guid tenantId)
    {
        return _configsByTenant.GetOrAdd(tenantId, CreateDefaultConfig);
    }

    public IntentScoringConfig UpdateConfig(Guid tenantId, UpdateIntentConfigDto update)
    {
        var current = GetConfig(tenantId);
        var updated = current with
        {
            WeightFirmographic = Math.Clamp(update.WeightFirmographic, 0.0, 1.0),
            WeightTechnographic = Math.Clamp(update.WeightTechnographic, 0.0, 1.0),
            WeightFirstPartyBehavioral = Math.Clamp(update.WeightFirstPartyBehavioral, 0.0, 1.0),
            WeightThirdPartySurge = Math.Clamp(update.WeightThirdPartySurge, 0.0, 1.0),
            DecayHalfLifeDays = Math.Max(1, update.DecayHalfLifeDays),
            MqlThresholdScore = Math.Clamp(update.MqlThresholdScore, 0.0, 100.0),
            SqlThresholdScore = Math.Clamp(update.SqlThresholdScore, 0.0, 100.0),
            TargetIndustries = update.TargetIndustries ?? current.TargetIndustries,
            TargetEmployeeTiers = update.TargetEmployeeTiers ?? current.TargetEmployeeTiers,
            TargetRegions = update.TargetRegions ?? current.TargetRegions,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _configsByTenant[tenantId] = updated;

        // Auto-recalculate leads to reflect updated config
        BatchRecalculate(tenantId);
        return updated;
    }

    public IReadOnlyList<LeadIntentRecord> GetLeads(Guid tenantId)
    {
        var tenantLeads = GetTenantLeadsDict(tenantId);
        return tenantLeads.Values.OrderByDescending(l => l.CompositeScore).ToList();
    }

    public LeadIntentRecord? GetLeadById(Guid leadId, Guid tenantId)
    {
        var tenantLeads = GetTenantLeadsDict(tenantId);
        return tenantLeads.TryGetValue(leadId, out var lead) ? lead : null;
    }

    public LeadIntentRecord IngestSignal(IngestSignalRequest request)
    {
        var tid = request.TenantId ?? DefaultTenantId;
        var tenantLeads = GetTenantLeadsDict(tid);

        if (!tenantLeads.TryGetValue(request.LeadId, out var existingLead))
        {
            throw new KeyNotFoundException($"Lead with ID '{request.LeadId}' was not found in tenant '{tid}'.");
        }

        var newSignal = new IntentSignal(
            Guid.NewGuid(),
            request.Type,
            request.Source,
            request.TopicOrResource,
            request.RawScore,
            request.Weight,
            request.OccurredAtUtc ?? DateTime.UtcNow,
            request.Metadata
        );

        var updatedSignals = new List<IntentSignal>(existingLead.ActiveSignals) { newSignal };
        var updatedLead = existingLead with { ActiveSignals = updatedSignals };

        var evaluated = EvaluateLeadRecord(updatedLead, GetConfig(tid), DateTime.UtcNow);
        tenantLeads[request.LeadId] = evaluated;
        return evaluated;
    }

    public LeadIntentRecord CreateOrUpdateLead(CreateOrUpdateLeadIntentRequest request)
    {
        var tid = request.TenantId ?? DefaultTenantId;
        var tenantLeads = GetTenantLeadsDict(tid);
        var leadId = request.LeadId ?? Guid.NewGuid();

        var existingSignals = tenantLeads.TryGetValue(leadId, out var existing)
            ? existing.ActiveSignals
            : new List<IntentSignal>();

        var rawLead = new LeadIntentRecord(
            leadId,
            tid,
            request.ContactEmail,
            request.ContactName,
            request.JobTitle,
            request.Firmographics,
            request.Technographics,
            existingSignals,
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0.0,
            ComputeIcpGrade(request.Firmographics.FitScore),
            PredictiveBuyingStage.Cold,
            LeadQualificationStage.Unqualified,
            NextActionRecommendation.MonitorAndLowTouchNewsletter,
            "Initial ingestion pending score calculation",
            DateTime.UtcNow,
            existing?.CreatedAtUtc ?? DateTime.UtcNow
        );

        var evaluated = EvaluateLeadRecord(rawLead, GetConfig(tid), DateTime.UtcNow);
        tenantLeads[leadId] = evaluated;
        return evaluated;
    }

    public LeadIntentRecord EvaluateLead(Guid leadId, Guid tenantId)
    {
        var tenantLeads = GetTenantLeadsDict(tenantId);
        if (!tenantLeads.TryGetValue(leadId, out var lead))
        {
            throw new KeyNotFoundException($"Lead with ID '{leadId}' was not found.");
        }

        var evaluated = EvaluateLeadRecord(lead, GetConfig(tenantId), DateTime.UtcNow);
        tenantLeads[leadId] = evaluated;
        return evaluated;
    }

    public BatchRecalculateResponse BatchRecalculate(Guid tenantId)
    {
        var tenantLeads = GetTenantLeadsDict(tenantId);
        var config = GetConfig(tenantId);
        var now = DateTime.UtcNow;

        int sqlCount = 0;
        int mqlCount = 0;
        int unqualifiedCount = 0;
        double scoreSum = 0.0;

        foreach (var kvp in tenantLeads)
        {
            var evaluated = EvaluateLeadRecord(kvp.Value, config, now);
            tenantLeads[kvp.Key] = evaluated;

            scoreSum += evaluated.CompositeScore;
            switch (evaluated.QualificationStage)
            {
                case LeadQualificationStage.SalesQualifiedLead:
                    sqlCount++;
                    break;
                case LeadQualificationStage.MarketingQualifiedLead:
                    mqlCount++;
                    break;
                default:
                    unqualifiedCount++;
                    break;
            }
        }

        var count = tenantLeads.Count;
        var avg = count > 0 ? Math.Round(scoreSum / count, 1) : 0.0;

        return new BatchRecalculateResponse(
            count,
            sqlCount,
            mqlCount,
            unqualifiedCount,
            avg,
            now
        );
    }

    public IntentScoringOverviewDto GetOverview(Guid tenantId)
    {
        var leads = GetLeads(tenantId);
        var config = GetConfig(tenantId);

        int sqlCount = 0;
        int mqlCount = 0;
        int unqCount = 0;
        int aCount = 0;
        int bCount = 0;
        int cCount = 0;
        int dCount = 0;
        double sum = 0;

        foreach (var l in leads)
        {
            sum += l.CompositeScore;
            if (l.QualificationStage == LeadQualificationStage.SalesQualifiedLead) sqlCount++;
            else if (l.QualificationStage == LeadQualificationStage.MarketingQualifiedLead) mqlCount++;
            else unqCount++;

            switch (l.IcpGrade)
            {
                case IcpGrade.GradeA: aCount++; break;
                case IcpGrade.GradeB: bCount++; break;
                case IcpGrade.GradeC: cCount++; break;
                case IcpGrade.GradeD: dCount++; break;
            }
        }

        var avg = leads.Count > 0 ? Math.Round(sum / leads.Count, 1) : 0.0;
        var topSurging = leads.OrderByDescending(l => l.CompositeScore).Take(5).ToList();

        return new IntentScoringOverviewDto(
            leads.Count,
            sqlCount,
            mqlCount,
            unqCount,
            aCount,
            bCount,
            cCount,
            dCount,
            avg,
            topSurging,
            config,
            DateTime.UtcNow
        );
    }

    public double CalculateDecayFactor(DateTime occurredAtUtc, int halfLifeDays, DateTime nowUtc)
    {
        if (halfLifeDays <= 0) return 1.0;
        var elapsedDays = Math.Max(0.0, (nowUtc - occurredAtUtc).TotalDays);
        return Math.Pow(0.5, elapsedDays / halfLifeDays);
    }

    public IcpGrade ComputeIcpGrade(double fitScore)
    {
        return fitScore switch
        {
            >= 90.0 => IcpGrade.GradeA,
            >= 75.0 => IcpGrade.GradeB,
            >= 50.0 => IcpGrade.GradeC,
            _ => IcpGrade.GradeD
        };
    }

    #region Internal Engine Logic

    private ConcurrentDictionary<Guid, LeadIntentRecord> GetTenantLeadsDict(Guid tenantId)
    {
        return _leadsByTenant.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, LeadIntentRecord>());
    }

    private IntentScoringConfig CreateDefaultConfig(Guid tenantId)
    {
        return new IntentScoringConfig(
            tenantId,
            WeightFirmographic: 0.25,
            WeightTechnographic: 0.20,
            WeightFirstPartyBehavioral: 0.35,
            WeightThirdPartySurge: 0.20,
            DecayHalfLifeDays: 14,
            MqlThresholdScore: 60.0,
            SqlThresholdScore: 80.0,
            TargetIndustries: ["B2B SaaS", "Fintech", "E-commerce & Logistics", "Healthcare Tech"],
            TargetEmployeeTiers: ["51-200", "201-1000", "1000+"],
            TargetRegions: ["MENA - Riyadh", "MENA - Dubai", "Europe - London", "North America - New York"],
            UpdatedAtUtc: DateTime.UtcNow
        );
    }

    private LeadIntentRecord EvaluateLeadRecord(LeadIntentRecord lead, IntentScoringConfig config, DateTime nowUtc)
    {
        // 1. Calculate Firmographic Fit & Grade
        var firmoScore = Math.Clamp(lead.Firmographics.FitScore, 0.0, 100.0);
        var icpGrade = ComputeIcpGrade(firmoScore);
        var updatedFirmographics = lead.Firmographics with { Grade = icpGrade };

        // 2. Calculate Technographic Score
        var technoScore = Math.Clamp(lead.Technographics.TechnographicScore, 0.0, 100.0);

        // 3. Process Active Intent Signals (First-party vs Third-party with Decay)
        double firstPartyRaw = 0.0;
        double firstPartyDecayed = 0.0;
        double thirdPartyRaw = 0.0;
        double thirdPartyDecayed = 0.0;

        foreach (var sig in lead.ActiveSignals)
        {
            var decay = CalculateDecayFactor(sig.OccurredAtUtc, config.DecayHalfLifeDays, nowUtc);
            var weightedContribution = sig.RawScore * sig.Weight;
            var decayedContribution = weightedContribution * decay;

            if (sig.Source == SignalSource.FirstParty)
            {
                firstPartyRaw += weightedContribution;
                firstPartyDecayed += decayedContribution;
            }
            else if (sig.Source == SignalSource.ThirdPartySurge)
            {
                thirdPartyRaw += weightedContribution;
                thirdPartyDecayed += decayedContribution;
            }
        }

        // Clamp signal buckets to 100
        var clampedFirstPartyRaw = Math.Min(100.0, Math.Round(firstPartyRaw, 1));
        var clampedFirstPartyDecayed = Math.Min(100.0, Math.Round(firstPartyDecayed, 1));
        var clampedThirdPartyRaw = Math.Min(100.0, Math.Round(thirdPartyRaw, 1));
        var clampedThirdPartyDecayed = Math.Min(100.0, Math.Round(thirdPartyDecayed, 1));

        // 4. Compute Weighted Components
        var firmoWeighted = Math.Round(firmoScore * config.WeightFirmographic, 2);
        var technoWeighted = Math.Round(technoScore * config.WeightTechnographic, 2);
        var firstPartyWeighted = Math.Round(clampedFirstPartyDecayed * config.WeightFirstPartyBehavioral, 2);
        var thirdPartyWeighted = Math.Round(clampedThirdPartyDecayed * config.WeightThirdPartySurge, 2);

        var composite = Math.Clamp(
            Math.Round(firmoWeighted + technoWeighted + firstPartyWeighted + thirdPartyWeighted, 1),
            0.0,
            100.0
        );

        var scoreBreakdown = new ScoreBreakdown(
            config.WeightFirmographic,
            firmoScore,
            firmoWeighted,
            config.WeightTechnographic,
            technoScore,
            technoWeighted,
            config.WeightFirstPartyBehavioral,
            clampedFirstPartyRaw,
            clampedFirstPartyDecayed,
            firstPartyWeighted,
            config.WeightThirdPartySurge,
            clampedThirdPartyRaw,
            clampedThirdPartyDecayed,
            thirdPartyWeighted,
            composite
        );

        // 5. Determine Qualification Stage (Auto-transitions based on thresholds & ICP)
        LeadQualificationStage stage;
        if (composite >= config.SqlThresholdScore && (icpGrade == IcpGrade.GradeA || icpGrade == IcpGrade.GradeB))
        {
            stage = LeadQualificationStage.SalesQualifiedLead;
        }
        else if (composite >= config.MqlThresholdScore)
        {
            stage = LeadQualificationStage.MarketingQualifiedLead;
        }
        else
        {
            stage = LeadQualificationStage.Unqualified;
        }

        // 6. Determine Predictive Buying Stage
        var buyingStage = composite switch
        {
            >= 85.0 => PredictiveBuyingStage.ReadyToBuy,
            >= 70.0 => PredictiveBuyingStage.HighIntent,
            >= 50.0 => PredictiveBuyingStage.Consideration,
            >= 30.0 => PredictiveBuyingStage.Awareness,
            _ => PredictiveBuyingStage.Cold
        };

        // 7. Determine Next Action Recommendation & Detailed Reasoning
        var (recommendation, reasoning) = DetermineNextAction(stage, buyingStage, icpGrade, composite, config);

        return lead with
        {
            Firmographics = updatedFirmographics,
            ScoreBreakdown = scoreBreakdown,
            CompositeScore = composite,
            IcpGrade = icpGrade,
            BuyingStage = buyingStage,
            QualificationStage = stage,
            RecommendedAction = recommendation,
            ActionReasoning = reasoning,
            LastEvaluatedAtUtc = nowUtc
        };
    }

    private static (NextActionRecommendation Recommendation, string Reasoning) DetermineNextAction(
        LeadQualificationStage stage,
        PredictiveBuyingStage buyingStage,
        IcpGrade grade,
        double score,
        IntentScoringConfig config)
    {
        if (stage == LeadQualificationStage.SalesQualifiedLead && score >= 85.0)
        {
            return (
                NextActionRecommendation.ScheduleExecutiveBriefing,
                $"Score {score:F1} (ReadyToBuy) with prime ICP {grade}. Immediate VIP executive briefing recommended."
            );
        }

        if (stage == LeadQualificationStage.SalesQualifiedLead)
        {
            return (
                NextActionRecommendation.RouteToSeniorSdrImmediate,
                $"Score {score:F1} exceeded SQL threshold ({config.SqlThresholdScore:F1}) with {grade} fit. Route to online SDR within 15 minutes."
            );
        }

        if (stage == LeadQualificationStage.MarketingQualifiedLead && buyingStage == PredictiveBuyingStage.Consideration)
        {
            return (
                NextActionRecommendation.TriggerAutomatedTechnicalNurture,
                $"MQL in Consideration stage with technical signals. Trigger targeted whitepaper & architecture cadence."
            );
        }

        if (stage == LeadQualificationStage.MarketingQualifiedLead)
        {
            return (
                NextActionRecommendation.EnrollInHighIntentCadence,
                $"Score {score:F1} qualified as MQL ({buyingStage}). Enroll in multi-touch outbound cadence."
            );
        }

        if (grade == IcpGrade.GradeD)
        {
            return (
                NextActionRecommendation.DisqualifyOrSuppress,
                $"ICP Grade D fit score. Disqualify or suppress to protect outbound domain reputation."
            );
        }

        return (
            NextActionRecommendation.MonitorAndLowTouchNewsletter,
            $"Score {score:F1} ({buyingStage}). Retain in low-frequency monthly product newsletter."
        );
    }

    private void SeedDefaultTenant(Guid tenantId)
    {
        var config = CreateDefaultConfig(tenantId);
        _configsByTenant[tenantId] = config;

        var dict = GetTenantLeadsDict(tenantId);
        var now = DateTime.UtcNow;

        // Lead 1: Aramco Digital Ventures (Ready to Buy, SQL, Grade A)
        var aramco = new LeadIntentRecord(
            AramcoLeadId,
            tenantId,
            "omar.otaibi@aramcodigital.com",
            "Omar Al-Otaibi",
            "VP Cloud Engineering",
            new FirmographicProfile(
                "aramcodigital.com",
                "Aramco Digital Ventures",
                "B2B SaaS",
                "1000+",
                "$100M+",
                "MENA - Riyadh",
                2021,
                96.0,
                IcpGrade.GradeA
            ),
            new TechnographicProfile(
                ["Salesforce Enterprise", "AWS Cloud", "Snowflake", "Kubernetes"],
                ["CRM", "Cloud Infrastructure", "Modern Analytics"],
                true,
                92.0
            ),
            new List<IntentSignal>
            {
                new(Guid.NewGuid(), IntentSignalType.DemoRequest, SignalSource.FirstParty, "/demo-request", 50.0, 1.5, now.AddHours(-3)),
                new(Guid.NewGuid(), IntentSignalType.PricingPageView, SignalSource.FirstParty, "/pricing/enterprise", 35.0, 1.2, now.AddHours(-8)),
                new(Guid.NewGuid(), IntentSignalType.BomboraSurgeTopic, SignalSource.ThirdPartySurge, "Omnichannel AI Orchestration", 45.0, 1.0, now.AddDays(-1))
            },
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0.0,
            IcpGrade.GradeA,
            PredictiveBuyingStage.Cold,
            LeadQualificationStage.Unqualified,
            NextActionRecommendation.MonitorAndLowTouchNewsletter,
            "",
            now,
            now.AddDays(-10)
        );

        // Lead 2: Maghreb Logistics Cloud (MQL, High Intent, Grade B)
        var maghreb = new LeadIntentRecord(
            MaghrebLeadId,
            tenantId,
            "selim.bensalem@maghreblogistics.tn",
            "Selim Ben Salem",
            "Chief Technology Officer",
            new FirmographicProfile(
                "maghreblogistics.tn",
                "Maghreb Logistics Cloud",
                "E-commerce & Logistics",
                "51-200",
                "$10M-$50M",
                "MENA - Tunis",
                2019,
                84.0,
                IcpGrade.GradeB
            ),
            new TechnographicProfile(
                ["PostgreSQL", "Shopify Plus", "Redis", "Next.js"],
                ["E-commerce", "Database", "Frontend"],
                false,
                82.0
            ),
            new List<IntentSignal>
            {
                new(Guid.NewGuid(), IntentSignalType.WhitepaperDownload, SignalSource.FirstParty, "Logistics COD Optimization Guide", 40.0, 1.2, now.AddDays(-1)),
                new(Guid.NewGuid(), IntentSignalType.DocumentationView, SignalSource.FirstParty, "/docs/api/courier-dispatch", 35.0, 1.0, now.AddDays(-2)),
                new(Guid.NewGuid(), IntentSignalType.G2CategorySearch, SignalSource.ThirdPartySurge, "Last-Mile Delivery Software", 40.0, 1.2, now.AddDays(-3))
            },
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0.0,
            IcpGrade.GradeB,
            PredictiveBuyingStage.Cold,
            LeadQualificationStage.Unqualified,
            NextActionRecommendation.MonitorAndLowTouchNewsletter,
            "",
            now,
            now.AddDays(-14)
        );

        // Lead 3: Al-Rajhi Enterprise FinTech (SQL, Ready to Buy, Grade A)
        var alrajhi = new LeadIntentRecord(
            AlRajhiLeadId,
            tenantId,
            "fatima.harbi@alrajhipayments.sa",
            "Fatima Al-Harbi",
            "Head of Digital Channels",
            new FirmographicProfile(
                "alrajhipayments.sa",
                "Al-Rajhi Digital Payments",
                "Fintech",
                "201-1000",
                "$50M-$250M",
                "MENA - Riyadh",
                2018,
                95.0,
                IcpGrade.GradeA
            ),
            new TechnographicProfile(
                ["Oracle Cloud", "Stripe Enterprise", "Apache Kafka", "Angular"],
                ["Payments", "Enterprise Cloud", "Streaming"],
                true,
                92.0
            ),
            new List<IntentSignal>
            {
                new(Guid.NewGuid(), IntentSignalType.DemoRequest, SignalSource.FirstParty, "/request-demo/financial-services", 60.0, 1.5, now.AddHours(-1)),
                new(Guid.NewGuid(), IntentSignalType.PricingPageView, SignalSource.FirstParty, "/pricing/custom-quote", 40.0, 1.2, now.AddHours(-4)),
                new(Guid.NewGuid(), IntentSignalType.ExecutiveHire, SignalSource.ThirdPartySurge, "New Chief Revenue Officer appointed", 50.0, 1.2, now.AddDays(-1))
            },
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0.0,
            IcpGrade.GradeA,
            PredictiveBuyingStage.Cold,
            LeadQualificationStage.Unqualified,
            NextActionRecommendation.MonitorAndLowTouchNewsletter,
            "",
            now,
            now.AddDays(-7)
        );

        // Lead 4: FinTech Global UK (MQL, Consideration, Grade B)
        var fintechUk = new LeadIntentRecord(
            FinTechUkLeadId,
            tenantId,
            "jsterling@fintechglobal.co.uk",
            "James Sterling",
            "VP Operations & Strategy",
            new FirmographicProfile(
                "fintechglobal.co.uk",
                "FinTech Global UK",
                "Fintech",
                "51-200",
                "$10M-$50M",
                "Europe - London",
                2020,
                78.0,
                IcpGrade.GradeB
            ),
            new TechnographicProfile(
                ["HubSpot CRM", "Segment CDP", "GCP", "React"],
                ["CRM", "CDP", "Cloud"],
                true,
                75.0
            ),
            new List<IntentSignal>
            {
                new(Guid.NewGuid(), IntentSignalType.G2CompetitorComparison, SignalSource.ThirdPartySurge, "SGrowing vs HubSpot Operations Hub", 40.0, 1.2, now.AddDays(-5)),
                new(Guid.NewGuid(), IntentSignalType.DocumentationView, SignalSource.FirstParty, "/docs/cdp/identity-stitching", 20.0, 1.0, now.AddDays(-6))
            },
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0.0,
            IcpGrade.GradeB,
            PredictiveBuyingStage.Cold,
            LeadQualificationStage.Unqualified,
            NextActionRecommendation.MonitorAndLowTouchNewsletter,
            "",
            now,
            now.AddDays(-20)
        );

        // Lead 5: Dubai Retail Group (Unqualified / Consideration, Grade C)
        var dubaiRetail = new LeadIntentRecord(
            DubaiRetailLeadId,
            tenantId,
            "znuaimi@dubairetail.ae",
            "Zayd Al-Nuaimi",
            "Chief Digital Officer",
            new FirmographicProfile(
                "dubairetail.ae",
                "Dubai Retail Group",
                "Retail",
                "1000+",
                "$50M-$250M",
                "MENA - Dubai",
                2012,
                68.0,
                IcpGrade.GradeC
            ),
            new TechnographicProfile(
                ["Magento", "SAP ERP", "Microsoft Azure"],
                ["E-commerce", "ERP", "Cloud"],
                false,
                60.0
            ),
            new List<IntentSignal>
            {
                new(Guid.NewGuid(), IntentSignalType.WebinarAttendance, SignalSource.FirstParty, "AI Agents in MENA E-Commerce", 25.0, 1.0, now.AddDays(-10))
            },
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0.0,
            IcpGrade.GradeC,
            PredictiveBuyingStage.Cold,
            LeadQualificationStage.Unqualified,
            NextActionRecommendation.MonitorAndLowTouchNewsletter,
            "",
            now,
            now.AddDays(-30)
        );

        // Lead 6: Legacy Hardware Supplies (Disqualified, Grade D)
        var legacy = new LeadIntentRecord(
            LegacyHardwareLeadId,
            tenantId,
            "dklein@legacyhardware.de",
            "David Klein",
            "Procurement Specialist",
            new FirmographicProfile(
                "legacyhardware.de",
                "Legacy Hardware Supplies",
                "Manufacturing",
                "11-50",
                "<$1M",
                "Europe - Berlin",
                1995,
                35.0,
                IcpGrade.GradeD
            ),
            new TechnographicProfile(
                ["On-Premise Exchange", "Access DB"],
                ["Legacy"],
                false,
                20.0
            ),
            new List<IntentSignal>
            {
                new(Guid.NewGuid(), IntentSignalType.DocumentationView, SignalSource.FirstParty, "/docs/quickstart", 10.0, 1.0, now.AddDays(-22))
            },
            new ScoreBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0.0,
            IcpGrade.GradeD,
            PredictiveBuyingStage.Cold,
            LeadQualificationStage.Unqualified,
            NextActionRecommendation.MonitorAndLowTouchNewsletter,
            "",
            now,
            now.AddDays(-40)
        );

        // Evaluate all initially
        dict[AramcoLeadId] = EvaluateLeadRecord(aramco, config, now);
        dict[MaghrebLeadId] = EvaluateLeadRecord(maghreb, config, now);
        dict[AlRajhiLeadId] = EvaluateLeadRecord(alrajhi, config, now);
        dict[FinTechUkLeadId] = EvaluateLeadRecord(fintechUk, config, now);
        dict[DubaiRetailLeadId] = EvaluateLeadRecord(dubaiRetail, config, now);
        dict[LegacyHardwareLeadId] = EvaluateLeadRecord(legacy, config, now);
    }

    #endregion
}
