using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Retention.CustomerHealth;

public interface ICustomerHealthService
{
    List<CustomerAccountHealthRecord> GetAccounts(Guid tenantId, HealthTier? tier = null, string? search = null);
    CustomerAccountHealthRecord? GetAccountById(Guid tenantId, Guid accountId);
    CustomerHealthRadarOverview GetOverview(Guid tenantId);
    CustomerAccountHealthRecord CreateAccount(CreateCustomerAccountRequest request);
    CustomerAccountHealthRecord? RecalculateAccountHealth(Guid tenantId, Guid accountId, UpdateAccountMetricsRequest? updatedMetrics = null);
    List<CustomerAccountHealthRecord> RecalculateAll(Guid tenantId);
    CsmPlaybookRecord? UpdatePlaybookStatus(Guid tenantId, Guid playbookId, PlaybookStatus status, string? notes = null);
    CsmPlaybookRecord? AddPlaybook(Guid tenantId, Guid accountId, CreatePlaybookRequest request);
    void Reset(Guid? tenantId = null);
}

public class CustomerHealthService : ICustomerHealthService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid SeedAccount1Id = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid SeedAccount2Id = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid SeedAccount3Id = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid SeedAccount4Id = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid SeedAccount5Id = Guid.Parse("20000000-0000-0000-0000-000000000005");

    // Weights: Adoption 35%, Sentiment 25%, Utilization 20%, Commercial 20%
    public const double AdoptionWeight = 0.35;
    public const double SentimentWeight = 0.25;
    public const double UtilizationWeight = 0.20;
    public const double CommercialWeight = 0.20;

    private readonly ConcurrentDictionary<Guid, CustomerAccountHealthRecord> _accounts = new();
    private readonly object _syncLock = new();

    public CustomerHealthService()
    {
        SeedDefaultData();
    }

    public void Reset(Guid? tenantId = null)
    {
        lock (_syncLock)
        {
            var targetTenant = tenantId ?? DefaultTenantId;
            var keysToRemove = _accounts.Values
                .Where(a => a.TenantId == targetTenant)
                .Select(a => a.AccountId)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _accounts.TryRemove(key, out _);
            }

            SeedDefaultData(targetTenant);
        }
    }

    public List<CustomerAccountHealthRecord> GetAccounts(Guid tenantId, HealthTier? tier = null, string? search = null)
    {
        var query = _accounts.Values.Where(a => a.TenantId == tenantId);

        if (tier.HasValue)
        {
            query = query.Where(a => a.HealthTier == tier.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim();
            query = query.Where(a =>
                a.AccountName.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                a.Domain.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                a.AssignedCsm.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                a.Industry.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(a => a.CompositeHealthScore)
            .ThenByDescending(a => a.AnnualRecurringRevenue)
            .ToList();
    }

    public CustomerAccountHealthRecord? GetAccountById(Guid tenantId, Guid accountId)
    {
        if (_accounts.TryGetValue(accountId, out var account) && account.TenantId == tenantId)
        {
            return account;
        }

        return null;
    }

    public CustomerHealthRadarOverview GetOverview(Guid tenantId)
    {
        var accounts = GetAccounts(tenantId);
        var totalAccounts = accounts.Count;
        var healthyCount = accounts.Count(a => a.HealthTier == HealthTier.Healthy);
        var atRiskCount = accounts.Count(a => a.HealthTier == HealthTier.AtRisk);
        var criticalCount = accounts.Count(a => a.HealthTier == HealthTier.Critical);
        var avgHealth = totalAccounts > 0 ? Math.Round(accounts.Average(a => a.CompositeHealthScore), 1) : 0.0;
        var totalArr = accounts.Sum(a => a.AnnualRecurringRevenue);
        var arrAtRisk = accounts.Where(a => a.HealthTier != HealthTier.Healthy).Sum(a => a.AnnualRecurringRevenue);
        var activePlaybooksCount = accounts.SelectMany(a => a.ActivePlaybooks).Count(p => p.Status == PlaybookStatus.Active || p.Status == PlaybookStatus.Suggested);

        return new CustomerHealthRadarOverview(
            TenantId: tenantId,
            TotalAccounts: totalAccounts,
            HealthyCount: healthyCount,
            AtRiskCount: atRiskCount,
            CriticalCount: criticalCount,
            AverageHealthScore: avgHealth,
            TotalArr: totalArr,
            ArrAtRisk: arrAtRisk,
            ActivePlaybookCount: activePlaybooksCount,
            Accounts: accounts
        );
    }

    public CustomerAccountHealthRecord CreateAccount(CreateCustomerAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccountName))
        {
            throw new ArgumentException("Account name is required.", nameof(request));
        }

        var accountId = Guid.NewGuid();
        var totalSeats = Math.Max(1, request.TotalSeats);
        var activeSeats = Math.Clamp(request.ActiveSeats, 0, totalSeats);
        var inactiveRatio = Math.Round((totalSeats - activeSeats) / (double)totalSeats, 4);

        var adoption = Math.Clamp(request.AdoptionScore, 0.0, 100.0);
        var sentiment = Math.Clamp(request.SentimentScore, 0.0, 100.0);
        var utilization = Math.Clamp(request.UtilizationScore, 0.0, 100.0);
        var commercial = Math.Clamp(request.CommercialScore, 0.0, 100.0);

        var composite = CalculateCompositeHealthScore(adoption, sentiment, utilization, commercial);
        var tier = DetermineHealthTier(composite);

        var triggers = DetectEarlyWarningRiskTriggers(
            npsScore: request.NpsScore,
            previousNpsScore: request.PreviousNpsScore,
            openTickets: request.OpenTicketsCount,
            urgentTickets: request.UrgentTicketsCount,
            inactiveRatio: inactiveRatio,
            totalSeats: totalSeats,
            activeSeats: activeSeats,
            daysUntilRenewal: request.DaysUntilRenewal,
            daysOverdueInvoice: request.DaysOverdueInvoice,
            compositeScore: composite
        );

        var churnProb = CalculateChurnProbability(composite, triggers.Count);

        var playbooks = GeneratePlaybooks(
            accountId: accountId,
            accountName: request.AccountName,
            tier: tier,
            compositeScore: composite,
            adoptionScore: adoption,
            sentimentScore: sentiment,
            commercialScore: commercial,
            inactiveRatio: inactiveRatio,
            arr: request.AnnualRecurringRevenue,
            daysUntilRenewal: request.DaysUntilRenewal,
            daysOverdueInvoice: request.DaysOverdueInvoice,
            urgentTickets: request.UrgentTicketsCount,
            triggers: triggers
        );

        var now = DateTime.UtcNow;
        var initialSnapshot = new HealthScoreSnapshotRecord(
            SnapshotId: Guid.NewGuid(),
            RecordedAtUtc: now,
            CompositeHealthScore: composite,
            AdoptionScore: adoption,
            SentimentScore: sentiment,
            UtilizationScore: utilization,
            CommercialScore: commercial,
            HealthTier: tier
        );

        var account = new CustomerAccountHealthRecord(
            AccountId: accountId,
            TenantId: request.TenantId,
            AccountName: request.AccountName.Trim(),
            Domain: request.Domain?.Trim() ?? string.Empty,
            Industry: request.Industry?.Trim() ?? "General Enterprise",
            AssignedCsm: request.AssignedCsm?.Trim() ?? "Unassigned CSM",
            AnnualRecurringRevenue: request.AnnualRecurringRevenue,
            TotalSeats: totalSeats,
            ActiveSeats: activeSeats,
            InactiveSeatRatio: inactiveRatio,
            NpsScore: request.NpsScore,
            PreviousNpsScore: request.PreviousNpsScore,
            OpenTicketsCount: Math.Max(0, request.OpenTicketsCount),
            UrgentTicketsCount: Math.Max(0, request.UrgentTicketsCount),
            DaysUntilRenewal: request.DaysUntilRenewal,
            DaysOverdueInvoice: Math.Max(0, request.DaysOverdueInvoice),
            AdoptionScore: adoption,
            SentimentScore: sentiment,
            UtilizationScore: utilization,
            CommercialScore: commercial,
            CompositeHealthScore: composite,
            HealthTier: tier,
            PredictedChurnProbability: churnProb,
            LastEvaluatedAtUtc: now,
            RiskTriggers: triggers,
            ActivePlaybooks: playbooks,
            HistoricalSnapshots: new List<HealthScoreSnapshotRecord> { initialSnapshot },
            Tags: request.Tags ?? new List<string>()
        );

        _accounts[accountId] = account;
        return account;
    }

    public CustomerAccountHealthRecord? RecalculateAccountHealth(Guid tenantId, Guid accountId, UpdateAccountMetricsRequest? updatedMetrics = null)
    {
        if (!_accounts.TryGetValue(accountId, out var current) || current.TenantId != tenantId)
        {
            return null;
        }

        lock (_syncLock)
        {
            var totalSeats = updatedMetrics?.TotalSeats ?? current.TotalSeats;
            totalSeats = Math.Max(1, totalSeats);

            var activeSeats = updatedMetrics?.ActiveSeats ?? current.ActiveSeats;
            activeSeats = Math.Clamp(activeSeats, 0, totalSeats);

            var inactiveRatio = Math.Round((totalSeats - activeSeats) / (double)totalSeats, 4);

            var nps = updatedMetrics?.NpsScore ?? current.NpsScore;
            var prevNps = updatedMetrics?.PreviousNpsScore ?? current.PreviousNpsScore;
            var openTickets = Math.Max(0, updatedMetrics?.OpenTicketsCount ?? current.OpenTicketsCount);
            var urgentTickets = Math.Max(0, updatedMetrics?.UrgentTicketsCount ?? current.UrgentTicketsCount);
            var daysUntilRenewal = updatedMetrics?.DaysUntilRenewal ?? current.DaysUntilRenewal;
            var daysOverdue = Math.Max(0, updatedMetrics?.DaysOverdueInvoice ?? current.DaysOverdueInvoice);
            var assignedCsm = updatedMetrics?.AssignedCsm?.Trim() ?? current.AssignedCsm;

            var adoption = Math.Clamp(updatedMetrics?.AdoptionScore ?? current.AdoptionScore, 0.0, 100.0);
            var sentiment = Math.Clamp(updatedMetrics?.SentimentScore ?? current.SentimentScore, 0.0, 100.0);
            var utilization = Math.Clamp(updatedMetrics?.UtilizationScore ?? current.UtilizationScore, 0.0, 100.0);
            var commercial = Math.Clamp(updatedMetrics?.CommercialScore ?? current.CommercialScore, 0.0, 100.0);

            var composite = CalculateCompositeHealthScore(adoption, sentiment, utilization, commercial);
            var tier = DetermineHealthTier(composite);

            var triggers = DetectEarlyWarningRiskTriggers(
                npsScore: nps,
                previousNpsScore: prevNps,
                openTickets: openTickets,
                urgentTickets: urgentTickets,
                inactiveRatio: inactiveRatio,
                totalSeats: totalSeats,
                activeSeats: activeSeats,
                daysUntilRenewal: daysUntilRenewal,
                daysOverdueInvoice: daysOverdue,
                compositeScore: composite
            );

            var churnProb = CalculateChurnProbability(composite, triggers.Count);

            var generatedPlaybooks = GeneratePlaybooks(
                accountId: accountId,
                accountName: current.AccountName,
                tier: tier,
                compositeScore: composite,
                adoptionScore: adoption,
                sentimentScore: sentiment,
                commercialScore: commercial,
                inactiveRatio: inactiveRatio,
                arr: current.AnnualRecurringRevenue,
                daysUntilRenewal: daysUntilRenewal,
                daysOverdueInvoice: daysOverdue,
                urgentTickets: urgentTickets,
                triggers: triggers
            );

            // Merge with existing playbooks (preserve in-progress/completed statuses)
            var mergedPlaybooks = new List<CsmPlaybookRecord>();
            var existingMap = current.ActivePlaybooks.ToDictionary(p => p.PlaybookType);

            foreach (var gen in generatedPlaybooks)
            {
                if (existingMap.TryGetValue(gen.PlaybookType, out var existing))
                {
                    // If existing is already active or completed, retain its status and notes
                    mergedPlaybooks.Add(existing with
                    {
                        Priority = gen.Priority,
                        RecommendedActions = gen.RecommendedActions
                    });
                }
                else
                {
                    mergedPlaybooks.Add(gen);
                }
            }

            // Also keep any custom playbooks not automatically generated
            foreach (var custom in current.ActivePlaybooks.Where(p => !generatedPlaybooks.Any(g => g.PlaybookType == p.PlaybookType)))
            {
                mergedPlaybooks.Add(custom);
            }

            var now = DateTime.UtcNow;
            var snapshots = new List<HealthScoreSnapshotRecord>(current.HistoricalSnapshots)
            {
                new(
                    SnapshotId: Guid.NewGuid(),
                    RecordedAtUtc: now,
                    CompositeHealthScore: composite,
                    AdoptionScore: adoption,
                    SentimentScore: sentiment,
                    UtilizationScore: utilization,
                    CommercialScore: commercial,
                    HealthTier: tier
                )
            };

            var updated = current with
            {
                TotalSeats = totalSeats,
                ActiveSeats = activeSeats,
                InactiveSeatRatio = inactiveRatio,
                NpsScore = nps,
                PreviousNpsScore = prevNps,
                OpenTicketsCount = openTickets,
                UrgentTicketsCount = urgentTickets,
                DaysUntilRenewal = daysUntilRenewal,
                DaysOverdueInvoice = daysOverdue,
                AssignedCsm = assignedCsm,
                AdoptionScore = adoption,
                SentimentScore = sentiment,
                UtilizationScore = utilization,
                CommercialScore = commercial,
                CompositeHealthScore = composite,
                HealthTier = tier,
                PredictedChurnProbability = churnProb,
                LastEvaluatedAtUtc = now,
                RiskTriggers = triggers,
                ActivePlaybooks = mergedPlaybooks,
                HistoricalSnapshots = snapshots
            };

            _accounts[accountId] = updated;
            return updated;
        }
    }

    public List<CustomerAccountHealthRecord> RecalculateAll(Guid tenantId)
    {
        var targetAccounts = _accounts.Values.Where(a => a.TenantId == tenantId).ToList();
        var results = new List<CustomerAccountHealthRecord>();

        foreach (var acc in targetAccounts)
        {
            var updated = RecalculateAccountHealth(tenantId, acc.AccountId);
            if (updated != null)
            {
                results.Add(updated);
            }
        }

        return results;
    }

    public CsmPlaybookRecord? UpdatePlaybookStatus(Guid tenantId, Guid playbookId, PlaybookStatus status, string? notes = null)
    {
        lock (_syncLock)
        {
            foreach (var acc in _accounts.Values.Where(a => a.TenantId == tenantId))
            {
                var playbookIndex = acc.ActivePlaybooks.FindIndex(p => p.PlaybookId == playbookId);
                if (playbookIndex >= 0)
                {
                    var currentPlaybook = acc.ActivePlaybooks[playbookIndex];
                    var completedAt = status == PlaybookStatus.Completed ? DateTime.UtcNow : currentPlaybook.CompletedAtUtc;
                    var updatedPlaybook = currentPlaybook with
                    {
                        Status = status,
                        Notes = notes ?? currentPlaybook.Notes,
                        CompletedAtUtc = completedAt
                    };

                    var updatedPlaybookList = new List<CsmPlaybookRecord>(acc.ActivePlaybooks);
                    updatedPlaybookList[playbookIndex] = updatedPlaybook;

                    _accounts[acc.AccountId] = acc with
                    {
                        ActivePlaybooks = updatedPlaybookList
                    };

                    return updatedPlaybook;
                }
            }

            return null;
        }
    }

    public CsmPlaybookRecord? AddPlaybook(Guid tenantId, Guid accountId, CreatePlaybookRequest request)
    {
        if (!_accounts.TryGetValue(accountId, out var account) || account.TenantId != tenantId)
        {
            return null;
        }

        lock (_syncLock)
        {
            var playbook = new CsmPlaybookRecord(
                PlaybookId: Guid.NewGuid(),
                AccountId: accountId,
                AccountName: account.AccountName,
                Title: request.Title.Trim(),
                PlaybookType: request.PlaybookType,
                Priority: request.Priority,
                Status: PlaybookStatus.Active,
                Objective: request.Objective.Trim(),
                AssignedRole: request.AssignedRole.Trim(),
                RecommendedActions: request.RecommendedActions ?? new List<string>(),
                Notes: request.Notes,
                CreatedAtUtc: DateTime.UtcNow,
                CompletedAtUtc: null
            );

            var updatedList = new List<CsmPlaybookRecord>(account.ActivePlaybooks) { playbook };
            _accounts[accountId] = account with { ActivePlaybooks = updatedList };

            return playbook;
        }
    }

    public static double CalculateCompositeHealthScore(double adoptionScore, double sentimentScore, double utilizationScore, double commercialScore)
    {
        var clampedAdoption = Math.Clamp(adoptionScore, 0.0, 100.0);
        var clampedSentiment = Math.Clamp(sentimentScore, 0.0, 100.0);
        var clampedUtilization = Math.Clamp(utilizationScore, 0.0, 100.0);
        var clampedCommercial = Math.Clamp(commercialScore, 0.0, 100.0);

        var composite = (clampedAdoption * AdoptionWeight) +
                        (clampedSentiment * SentimentWeight) +
                        (clampedUtilization * UtilizationWeight) +
                        (clampedCommercial * CommercialWeight);

        return Math.Round(composite, 1);
    }

    public static HealthTier DetermineHealthTier(double compositeScore)
    {
        if (compositeScore >= 75.0)
        {
            return HealthTier.Healthy;
        }

        if (compositeScore >= 50.0)
        {
            return HealthTier.AtRisk;
        }

        return HealthTier.Critical;
    }

    public static double CalculateChurnProbability(double compositeScore, int riskTriggerCount)
    {
        double baseProb;
        if (compositeScore >= 75.0)
        {
            // Low risk: 1% to 20%
            baseProb = (100.0 - compositeScore) * 0.005 + (riskTriggerCount * 0.02);
            return Math.Round(Math.Clamp(baseProb, 0.01, 0.25), 2);
        }

        if (compositeScore >= 50.0)
        {
            // At risk: 25% to 65%
            baseProb = 0.25 + ((75.0 - compositeScore) * 0.012) + (riskTriggerCount * 0.04);
            return Math.Round(Math.Clamp(baseProb, 0.25, 0.65), 2);
        }

        // Critical: 65% to 98%
        baseProb = 0.65 + ((50.0 - compositeScore) * 0.006) + (riskTriggerCount * 0.04);
        return Math.Round(Math.Clamp(baseProb, 0.65, 0.98), 2);
    }

    public static List<EarlyWarningRiskTrigger> DetectEarlyWarningRiskTriggers(
        int npsScore,
        int previousNpsScore,
        int openTickets,
        int urgentTickets,
        double inactiveRatio,
        int totalSeats,
        int activeSeats,
        int daysUntilRenewal,
        int daysOverdueInvoice,
        double compositeScore)
    {
        var triggers = new List<EarlyWarningRiskTrigger>();
        var now = DateTime.UtcNow;

        // 1. NPS Drop Trigger (negative or >= 20 point drop)
        if (npsScore <= 0)
        {
            triggers.Add(new EarlyWarningRiskTrigger(
                TriggerId: Guid.NewGuid(),
                TriggerType: RiskTriggerType.NpsDrop,
                Severity: RiskSeverity.Critical,
                Description: $"Negative NPS score ({npsScore}). Client operates as active detractor.",
                CurrentMetricValue: npsScore,
                Threshold: 0,
                DetectedAtUtc: now
            ));
        }
        else if (previousNpsScore - npsScore >= 20)
        {
            var drop = previousNpsScore - npsScore;
            triggers.Add(new EarlyWarningRiskTrigger(
                TriggerId: Guid.NewGuid(),
                TriggerType: RiskTriggerType.NpsDrop,
                Severity: RiskSeverity.High,
                Description: $"Severe NPS decline of {drop} points (from {previousNpsScore} down to {npsScore}).",
                CurrentMetricValue: npsScore,
                Threshold: previousNpsScore - 20,
                DetectedAtUtc: now
            ));
        }

        // 2. Ticket Spike Trigger (open >= 5 or urgent >= 1)
        if (openTickets >= 5 || urgentTickets >= 2)
        {
            var severity = urgentTickets >= 2 ? RiskSeverity.Critical : RiskSeverity.High;
            triggers.Add(new EarlyWarningRiskTrigger(
                TriggerId: Guid.NewGuid(),
                TriggerType: RiskTriggerType.TicketSpike,
                Severity: severity,
                Description: $"Support ticket escalation spike: {openTickets} open tickets with {urgentTickets} urgent escalations.",
                CurrentMetricValue: openTickets,
                Threshold: 5,
                DetectedAtUtc: now
            ));
        }
        else if (urgentTickets == 1)
        {
            triggers.Add(new EarlyWarningRiskTrigger(
                TriggerId: Guid.NewGuid(),
                TriggerType: RiskTriggerType.TicketSpike,
                Severity: RiskSeverity.Medium,
                Description: "High-priority support escalation active without technical SWAT assignment.",
                CurrentMetricValue: urgentTickets,
                Threshold: 1,
                DetectedAtUtc: now
            ));
        }

        // 3. Inactive Seat Ratio Trigger (>= 35%)
        if (inactiveRatio >= 0.50)
        {
            triggers.Add(new EarlyWarningRiskTrigger(
                TriggerId: Guid.NewGuid(),
                TriggerType: RiskTriggerType.InactiveSeatRatio,
                Severity: RiskSeverity.Critical,
                Description: $"Critical seat dormancy: {inactiveRatio:P0} of licensed seats are inactive ({totalSeats - activeSeats}/{totalSeats} seats idle).",
                CurrentMetricValue: Math.Round(inactiveRatio * 100, 1),
                Threshold: 50.0,
                DetectedAtUtc: now
            ));
        }
        else if (inactiveRatio >= 0.35)
        {
            triggers.Add(new EarlyWarningRiskTrigger(
                TriggerId: Guid.NewGuid(),
                TriggerType: RiskTriggerType.InactiveSeatRatio,
                Severity: RiskSeverity.High,
                Description: $"Elevated inactive seat ratio: {inactiveRatio:P0} of seats idle ({totalSeats - activeSeats}/{totalSeats} unused).",
                CurrentMetricValue: Math.Round(inactiveRatio * 100, 1),
                Threshold: 35.0,
                DetectedAtUtc: now
            ));
        }

        // 4. Contract Renewal Risk Trigger (<= 60 days with sub-healthy score)
        if (daysUntilRenewal <= 60 && compositeScore < 75.0)
        {
            var severity = compositeScore < 50.0 ? RiskSeverity.Critical : RiskSeverity.High;
            triggers.Add(new EarlyWarningRiskTrigger(
                TriggerId: Guid.NewGuid(),
                TriggerType: RiskTriggerType.ContractRenewalRisk,
                Severity: severity,
                Description: $"Contract renewal approaching in {daysUntilRenewal} days with impaired health ({compositeScore:F1}).",
                CurrentMetricValue: daysUntilRenewal,
                Threshold: 60,
                DetectedAtUtc: now
            ));
        }

        // 5. Payment Overdue Trigger (>= 30 days)
        if (daysOverdueInvoice >= 30)
        {
            var severity = daysOverdueInvoice >= 45 ? RiskSeverity.Critical : RiskSeverity.High;
            triggers.Add(new EarlyWarningRiskTrigger(
                TriggerId: Guid.NewGuid(),
                TriggerType: RiskTriggerType.PaymentOverdue,
                Severity: severity,
                Description: $"Unsettled accounts receivable: payment overdue by {daysOverdueInvoice} days.",
                CurrentMetricValue: daysOverdueInvoice,
                Threshold: 30,
                DetectedAtUtc: now
            ));
        }

        return triggers;
    }

    public static List<CsmPlaybookRecord> GeneratePlaybooks(
        Guid accountId,
        string accountName,
        HealthTier tier,
        double compositeScore,
        double adoptionScore,
        double sentimentScore,
        double commercialScore,
        double inactiveRatio,
        decimal arr,
        int daysUntilRenewal,
        int daysOverdueInvoice,
        int urgentTickets,
        List<EarlyWarningRiskTrigger> triggers)
    {
        var playbooks = new List<CsmPlaybookRecord>();
        var now = DateTime.UtcNow;

        var hasNpsDrop = triggers.Any(t => t.TriggerType == RiskTriggerType.NpsDrop);
        var hasTicketSpike = triggers.Any(t => t.TriggerType == RiskTriggerType.TicketSpike);

        // 1. Executive sponsor check-in
        if (tier == HealthTier.Critical || sentimentScore < 50.0 || hasNpsDrop || (arr >= 50_000m && tier == HealthTier.AtRisk))
        {
            playbooks.Add(new CsmPlaybookRecord(
                PlaybookId: Guid.NewGuid(),
                AccountId: accountId,
                AccountName: accountName,
                Title: "Executive sponsor check-in",
                PlaybookType: PlaybookType.ExecutiveSponsorCheckin,
                Priority: tier == HealthTier.Critical ? PlaybookPriority.Urgent : PlaybookPriority.High,
                Status: PlaybookStatus.Suggested,
                Objective: "Restore executive relationship, realign on business goals, and clear strategic escalation road-blocks.",
                AssignedRole: "VP of Customer Success / Executive CSM",
                RecommendedActions: new List<string>
                {
                    "Convene 30-minute urgent alignment sync with primary VP/C-suite sponsor.",
                    "Review customer ROI scorecard and present targeted executive remediation roadmap.",
                    "Designate dedicated senior solutions engineer for ongoing sprint reviews."
                },
                Notes: "Auto-generated by Retention Radar due to critical tier or executive sentiment vulnerability.",
                CreatedAtUtc: now,
                CompletedAtUtc: null
            ));
        }

        // 2. Adoption workshop
        if (adoptionScore < 60.0 || inactiveRatio >= 0.35)
        {
            playbooks.Add(new CsmPlaybookRecord(
                PlaybookId: Guid.NewGuid(),
                AccountId: accountId,
                AccountName: accountName,
                Title: "Adoption workshop",
                PlaybookType: PlaybookType.AdoptionWorkshop,
                Priority: inactiveRatio >= 0.50 || adoptionScore < 45.0 ? PlaybookPriority.High : PlaybookPriority.Medium,
                Status: PlaybookStatus.Suggested,
                Objective: "Drive user onboarding and feature activation to eliminate seat dormancy and increase daily active workflows.",
                AssignedRole: "Lead Customer Success Manager",
                RecommendedActions: new List<string>
                {
                    "Conduct tailored 45-minute interactive workflow training session with department leads.",
                    "Deploy in-app role-based onboarding checklists and guided tooltips for inactive users.",
                    "Provide customized automation template library for immediate time-to-value."
                },
                Notes: "Auto-generated by Retention Radar to reverse user churn and dormant license utilization.",
                CreatedAtUtc: now,
                CompletedAtUtc: null
            ));
        }

        // 3. Emergency discount review
        if (commercialScore < 50.0 || (daysUntilRenewal <= 60 && tier != HealthTier.Healthy) || daysOverdueInvoice >= 30)
        {
            playbooks.Add(new CsmPlaybookRecord(
                PlaybookId: Guid.NewGuid(),
                AccountId: accountId,
                AccountName: accountName,
                Title: "Emergency discount review",
                PlaybookType: PlaybookType.EmergencyDiscountReview,
                Priority: tier == HealthTier.Critical || daysUntilRenewal <= 30 ? PlaybookPriority.Urgent : PlaybookPriority.High,
                Status: PlaybookStatus.Suggested,
                Objective: "Mitigate impending commercial churn through flexible terms, multi-year incentives, or billing restructuring.",
                AssignedRole: "Commercial Deal Desk & Customer Success Director",
                RecommendedActions: new List<string>
                {
                    "Prepare flexible contract restructuring proposal (multi-year incentive or quarterly payment frequency).",
                    "Consult Commercial Deal Desk & VP Finance for emergency concession approval.",
                    "Deliver value-add complimentary tier add-on during contract renewal window."
                },
                Notes: "Auto-generated by Retention Radar to protect expiring ARR and resolve overdue commercial friction.",
                CreatedAtUtc: now,
                CompletedAtUtc: null
            ));
        }

        // 4. Technical SWAT triage
        if (hasTicketSpike || urgentTickets > 0)
        {
            playbooks.Add(new CsmPlaybookRecord(
                PlaybookId: Guid.NewGuid(),
                AccountId: accountId,
                AccountName: accountName,
                Title: "Technical SWAT triage",
                PlaybookType: PlaybookType.TechnicalSwatTriage,
                Priority: urgentTickets >= 2 ? PlaybookPriority.Urgent : PlaybookPriority.High,
                Status: PlaybookStatus.Suggested,
                Objective: "Resolve open support tickets and stabilize technical integration to prevent platform churn.",
                AssignedRole: "Principal Solutions Architect / SWAT Lead",
                RecommendedActions: new List<string>
                {
                    "Dispatch Tier 3 SWAT engineer to resolve open support tickets within 24 hours.",
                    "Audit customer webhook latency and API failure error logs.",
                    "Establish private Slack/Teams incident bridge for real-time customer updates."
                },
                Notes: "Auto-generated by Retention Radar due to active support spike and SLA breach hazard.",
                CreatedAtUtc: now,
                CompletedAtUtc: null
            ));
        }

        return playbooks;
    }

    private void SeedDefaultData(Guid? targetTenant = null)
    {
        var tenantId = targetTenant ?? DefaultTenantId;
        var now = DateTime.UtcNow;

        // Seed Account 1: Healthy Enterprise
        // 92*0.35 + 88*0.25 + 94*0.20 + 95*0.20 = 32.2 + 22.0 + 18.8 + 19.0 = 92.0
        var acc1 = new CustomerAccountHealthRecord(
            AccountId: SeedAccount1Id,
            TenantId: tenantId,
            AccountName: "Aramco Digital Energy",
            Domain: "aramcodigital.sa",
            Industry: "Energy & Utilities",
            AssignedCsm: "Leila Al-Husseini",
            AnnualRecurringRevenue: 240_000m,
            TotalSeats: 150,
            ActiveSeats: 142,
            InactiveSeatRatio: 0.0533,
            NpsScore: 68,
            PreviousNpsScore: 65,
            OpenTicketsCount: 1,
            UrgentTicketsCount: 0,
            DaysUntilRenewal: 180,
            DaysOverdueInvoice: 0,
            AdoptionScore: 92.0,
            SentimentScore: 88.0,
            UtilizationScore: 94.0,
            CommercialScore: 95.0,
            CompositeHealthScore: 92.0,
            HealthTier: HealthTier.Healthy,
            PredictedChurnProbability: 0.04,
            LastEvaluatedAtUtc: now.AddHours(-2),
            RiskTriggers: new List<EarlyWarningRiskTrigger>(),
            ActivePlaybooks: new List<CsmPlaybookRecord>(),
            HistoricalSnapshots: new List<HealthScoreSnapshotRecord>
            {
                new(Guid.NewGuid(), now.AddDays(-60), 89.0, 90.0, 85.0, 92.0, 92.0, HealthTier.Healthy),
                new(Guid.NewGuid(), now.AddDays(-30), 91.0, 91.0, 87.0, 93.0, 94.0, HealthTier.Healthy),
                new(Guid.NewGuid(), now.AddHours(-2), 92.0, 92.0, 88.0, 94.0, 95.0, HealthTier.Healthy)
            },
            Tags: new List<string> { "Enterprise", "GCC", "ExpansionTarget" }
        );

        // Seed Account 2: AtRisk Mid-Market (Inactive seats 40%, NPS drop, renewal in 45 days)
        // 58*0.35 + 60*0.25 + 60*0.20 + 75*0.20 = 20.3 + 15.0 + 12.0 + 15.0 = 62.3
        var triggers2 = DetectEarlyWarningRiskTriggers(
            npsScore: 22,
            previousNpsScore: 45,
            openTickets: 3,
            urgentTickets: 0,
            inactiveRatio: 0.40,
            totalSeats: 80,
            activeSeats: 48,
            daysUntilRenewal: 45,
            daysOverdueInvoice: 0,
            compositeScore: 62.3
        );
        var playbooks2 = GeneratePlaybooks(
            accountId: SeedAccount2Id,
            accountName: "Al-Rajhi Retail Tech",
            tier: HealthTier.AtRisk,
            compositeScore: 62.3,
            adoptionScore: 58.0,
            sentimentScore: 60.0,
            commercialScore: 75.0,
            inactiveRatio: 0.40,
            arr: 120_000m,
            daysUntilRenewal: 45,
            daysOverdueInvoice: 0,
            urgentTickets: 0,
            triggers: triggers2
        );
        var acc2 = new CustomerAccountHealthRecord(
            AccountId: SeedAccount2Id,
            TenantId: tenantId,
            AccountName: "Al-Rajhi Retail Tech",
            Domain: "alrajhiretail.com",
            Industry: "Financial Retail & FinTech",
            AssignedCsm: "Omar Al-Farhan",
            AnnualRecurringRevenue: 120_000m,
            TotalSeats: 80,
            ActiveSeats: 48,
            InactiveSeatRatio: 0.40,
            NpsScore: 22,
            PreviousNpsScore: 45,
            OpenTicketsCount: 3,
            UrgentTicketsCount: 0,
            DaysUntilRenewal: 45,
            DaysOverdueInvoice: 0,
            AdoptionScore: 58.0,
            SentimentScore: 60.0,
            UtilizationScore: 60.0,
            CommercialScore: 75.0,
            CompositeHealthScore: 62.3,
            HealthTier: HealthTier.AtRisk,
            PredictedChurnProbability: 0.38,
            LastEvaluatedAtUtc: now.AddHours(-5),
            RiskTriggers: triggers2,
            ActivePlaybooks: playbooks2,
            HistoricalSnapshots: new List<HealthScoreSnapshotRecord>
            {
                new(Guid.NewGuid(), now.AddDays(-60), 78.5, 76.0, 80.0, 80.0, 80.0, HealthTier.Healthy),
                new(Guid.NewGuid(), now.AddDays(-30), 70.0, 68.0, 72.0, 70.0, 75.0, HealthTier.AtRisk),
                new(Guid.NewGuid(), now.AddHours(-5), 62.3, 58.0, 60.0, 60.0, 75.0, HealthTier.AtRisk)
            },
            Tags: new List<string> { "FinTech", "RenewalRisk", "CSMReview" }
        );

        // Seed Account 3: Critical Account (Ticket spike, NPS detractor, dormant seats 63%, overdue)
        // 40*0.35 + 30*0.25 + 36*0.20 + 35*0.20 = 14.0 + 7.5 + 7.2 + 7.0 = 35.7
        var triggers3 = DetectEarlyWarningRiskTriggers(
            npsScore: -15,
            previousNpsScore: 20,
            openTickets: 7,
            urgentTickets: 3,
            inactiveRatio: 0.6333,
            totalSeats: 60,
            activeSeats: 22,
            daysUntilRenewal: 28,
            daysOverdueInvoice: 42,
            compositeScore: 35.7
        );
        var playbooks3 = GeneratePlaybooks(
            accountId: SeedAccount3Id,
            accountName: "Dubai Logistics One",
            tier: HealthTier.Critical,
            compositeScore: 35.7,
            adoptionScore: 40.0,
            sentimentScore: 30.0,
            commercialScore: 35.0,
            inactiveRatio: 0.6333,
            arr: 85_000m,
            daysUntilRenewal: 28,
            daysOverdueInvoice: 42,
            urgentTickets: 3,
            triggers: triggers3
        );
        var acc3 = new CustomerAccountHealthRecord(
            AccountId: SeedAccount3Id,
            TenantId: tenantId,
            AccountName: "Dubai Logistics One",
            Domain: "dubailogistics1.ae",
            Industry: "Supply Chain & Logistics",
            AssignedCsm: "Rami Al-Ghamdi",
            AnnualRecurringRevenue: 85_000m,
            TotalSeats: 60,
            ActiveSeats: 22,
            InactiveSeatRatio: 0.6333,
            NpsScore: -15,
            PreviousNpsScore: 20,
            OpenTicketsCount: 7,
            UrgentTicketsCount: 3,
            DaysUntilRenewal: 28,
            DaysOverdueInvoice: 42,
            AdoptionScore: 40.0,
            SentimentScore: 30.0,
            UtilizationScore: 36.0,
            CommercialScore: 35.0,
            CompositeHealthScore: 35.7,
            HealthTier: HealthTier.Critical,
            PredictedChurnProbability: 0.85,
            LastEvaluatedAtUtc: now.AddMinutes(-45),
            RiskTriggers: triggers3,
            ActivePlaybooks: playbooks3,
            HistoricalSnapshots: new List<HealthScoreSnapshotRecord>
            {
                new(Guid.NewGuid(), now.AddDays(-60), 65.0, 62.0, 68.0, 65.0, 68.0, HealthTier.AtRisk),
                new(Guid.NewGuid(), now.AddDays(-30), 48.0, 48.0, 45.0, 50.0, 52.0, HealthTier.Critical),
                new(Guid.NewGuid(), now.AddMinutes(-45), 35.7, 40.0, 30.0, 36.0, 35.0, HealthTier.Critical)
            },
            Tags: new List<string> { "CriticalChurn", "EscalationDesk", "OverdueInvoice" }
        );

        // Seed Account 4: Healthy High-Growth Fleet Ops
        // 82*0.35 + 78*0.25 + 88*0.20 + 85*0.20 = 28.7 + 19.5 + 17.6 + 17.0 = 82.8
        var acc4 = new CustomerAccountHealthRecord(
            AccountId: SeedAccount4Id,
            TenantId: tenantId,
            AccountName: "Careem Fleet Ops",
            Domain: "careemfleet.com",
            Industry: "Mobility & Micro-Logistics",
            AssignedCsm: "Leila Al-Husseini",
            AnnualRecurringRevenue: 180_000m,
            TotalSeats: 120,
            ActiveSeats: 108,
            InactiveSeatRatio: 0.10,
            NpsScore: 55,
            PreviousNpsScore: 50,
            OpenTicketsCount: 2,
            UrgentTicketsCount: 0,
            DaysUntilRenewal: 210,
            DaysOverdueInvoice: 0,
            AdoptionScore: 82.0,
            SentimentScore: 78.0,
            UtilizationScore: 88.0,
            CommercialScore: 85.0,
            CompositeHealthScore: 82.8,
            HealthTier: HealthTier.Healthy,
            PredictedChurnProbability: 0.09,
            LastEvaluatedAtUtc: now.AddHours(-12),
            RiskTriggers: new List<EarlyWarningRiskTrigger>(),
            ActivePlaybooks: new List<CsmPlaybookRecord>(),
            HistoricalSnapshots: new List<HealthScoreSnapshotRecord>
            {
                new(Guid.NewGuid(), now.AddDays(-30), 80.0, 80.0, 75.0, 85.0, 84.0, HealthTier.Healthy),
                new(Guid.NewGuid(), now.AddHours(-12), 82.8, 82.0, 78.0, 88.0, 85.0, HealthTier.Healthy)
            },
            Tags: new List<string> { "GrowthAccount", "Logistics", "Loyal" }
        );

        // Seed Account 5: AtRisk E-Commerce (Adoption 52%, Inactive seats 42.2%)
        // 52*0.35 + 54*0.25 + 55*0.20 + 58*0.20 = 18.2 + 13.5 + 11.0 + 11.6 = 54.3
        var triggers5 = DetectEarlyWarningRiskTriggers(
            npsScore: 10,
            previousNpsScore: 25,
            openTickets: 4,
            urgentTickets: 1,
            inactiveRatio: 0.4222,
            totalSeats: 45,
            activeSeats: 26,
            daysUntilRenewal: 95,
            daysOverdueInvoice: 15,
            compositeScore: 54.3
        );
        var playbooks5 = GeneratePlaybooks(
            accountId: SeedAccount5Id,
            accountName: "Cairo E-Commerce Hub",
            tier: HealthTier.AtRisk,
            compositeScore: 54.3,
            adoptionScore: 52.0,
            sentimentScore: 54.0,
            commercialScore: 58.0,
            inactiveRatio: 0.4222,
            arr: 64_000m,
            daysUntilRenewal: 95,
            daysOverdueInvoice: 15,
            urgentTickets: 1,
            triggers: triggers5
        );
        var acc5 = new CustomerAccountHealthRecord(
            AccountId: SeedAccount5Id,
            TenantId: tenantId,
            AccountName: "Cairo E-Commerce Hub",
            Domain: "cairohub.eg",
            Industry: "Omnichannel Retail",
            AssignedCsm: "Omar Al-Farhan",
            AnnualRecurringRevenue: 64_000m,
            TotalSeats: 45,
            ActiveSeats: 26,
            InactiveSeatRatio: 0.4222,
            NpsScore: 10,
            PreviousNpsScore: 25,
            OpenTicketsCount: 4,
            UrgentTicketsCount: 1,
            DaysUntilRenewal: 95,
            DaysOverdueInvoice: 15,
            AdoptionScore: 52.0,
            SentimentScore: 54.0,
            UtilizationScore: 55.0,
            CommercialScore: 58.0,
            CompositeHealthScore: 54.3,
            HealthTier: HealthTier.AtRisk,
            PredictedChurnProbability: 0.45,
            LastEvaluatedAtUtc: now.AddHours(-1),
            RiskTriggers: triggers5,
            ActivePlaybooks: playbooks5,
            HistoricalSnapshots: new List<HealthScoreSnapshotRecord>
            {
                new(Guid.NewGuid(), now.AddDays(-30), 60.0, 58.0, 60.0, 62.0, 64.0, HealthTier.AtRisk),
                new(Guid.NewGuid(), now.AddHours(-1), 54.3, 52.0, 54.0, 55.0, 58.0, HealthTier.AtRisk)
            },
            Tags: new List<string> { "Retail", "AdoptionRisk", "CSMReview" }
        );

        _accounts[acc1.AccountId] = acc1;
        _accounts[acc2.AccountId] = acc2;
        _accounts[acc3.AccountId] = acc3;
        _accounts[acc4.AccountId] = acc4;
        _accounts[acc5.AccountId] = acc5;
    }
}
