using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Contracts.ChurnRescue;

public class ChurnRescueEngine
{
    private readonly ConcurrentDictionary<string, AccountHealthSignal> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RenewalAgreement> _agreements = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PlaybookExecutionResult> _executions = new(StringComparer.OrdinalIgnoreCase);

    public ChurnRescueEngine()
    {
        SeedAccounts();
    }

    private void SeedAccounts()
    {
        var acc1 = new AccountHealthSignal(
            AccountId: "acc_globex_corp",
            AccountName: "Globex Logistics Systems",
            AnnualRecurringRevenue: 124000m,
            ContractExpiresAtUtc: DateTime.UtcNow.AddDays(42),
            UsageDropPercentage: 48.5,
            LoginFrequencyScore: 22,
            SupportTicketSentiment: 0.25,
            ChampionTurnoverFlag: true
        );

        var acc2 = new AccountHealthSignal(
            AccountId: "acc_novus_health",
            AccountName: "Novus Digital Health",
            AnnualRecurringRevenue: 86000m,
            ContractExpiresAtUtc: DateTime.UtcNow.AddDays(85),
            UsageDropPercentage: 12.0,
            LoginFrequencyScore: 78,
            SupportTicketSentiment: 0.85,
            ChampionTurnoverFlag: false
        );

        var acc3 = new AccountHealthSignal(
            AccountId: "acc_zenith_media",
            AccountName: "Zenith OmniMedia Group",
            AnnualRecurringRevenue: 215000m,
            ContractExpiresAtUtc: DateTime.UtcNow.AddDays(28),
            UsageDropPercentage: 35.0,
            LoginFrequencyScore: 40,
            SupportTicketSentiment: 0.40,
            ChampionTurnoverFlag: true
        );

        _accounts[acc1.AccountId] = acc1;
        _accounts[acc2.AccountId] = acc2;
        _accounts[acc3.AccountId] = acc3;
    }

    public Task<IReadOnlyList<ChurnRiskAssessment>> GetRiskRadarAsync(CancellationToken ct = default)
    {
        var radar = _accounts.Values
            .Select(CalculateRiskAssessment)
            .OrderByDescending(r => r.ChurnPropensityScore)
            .ToList();

        return Task.FromResult<IReadOnlyList<ChurnRiskAssessment>>(radar);
    }

    public Task<ChurnRiskAssessment> AssessAccountRiskAsync(string accountId, CancellationToken ct = default)
    {
        if (!_accounts.TryGetValue(accountId, out var account))
        {
            throw new KeyNotFoundException($"Account '{accountId}' not found.");
        }

        var assessment = CalculateRiskAssessment(account);
        return Task.FromResult(assessment);
    }

    public Task<PlaybookExecutionResult> ExecutePlaybookAsync(TriggerPlaybookRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_accounts.TryGetValue(request.AccountId, out var account))
        {
            throw new KeyNotFoundException($"Account '{request.AccountId}' not found.");
        }

        var playbookId = $"play_{Guid.NewGuid():N}";
        var agreementUrl = $"https://app.growth-os.cloud/agreements/escrow_{request.AccountId}";

        var summary = request.PlaybookType switch
        {
            "DiscountLock" => $"Dispatched automated {request.CustomDiscountPercent ?? 15}% 2-year price-lock escrow proposal.",
            "ExecutiveSponsorSync" => "Generated executive VIP sponsor sync task and calendar scheduling link.",
            "UsageCredits" => "Injected $5,000 in promotional platform usage credits for customer success recovery.",
            _ => "Initiated proactive customer retention sequence."
        };

        var execution = new PlaybookExecutionResult(
            PlaybookId: playbookId,
            AccountId: account.AccountId,
            PlaybookType: request.PlaybookType,
            ActionSummary: summary,
            EscrowAgreementUrl: agreementUrl,
            Status: "Initiated",
            ExecutedAtUtc: DateTime.UtcNow
        );

        _executions[playbookId] = execution;
        return Task.FromResult(execution);
    }

    public Task<RenewalAgreement> GenerateRenewalAgreementAsync(GenerateRenewalAgreementRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_accounts.TryGetValue(request.AccountId, out var account))
        {
            throw new KeyNotFoundException($"Account '{request.AccountId}' not found.");
        }

        var agreementId = $"agr_{Guid.NewGuid():N}";
        var discountedValue = request.AnnualContractValue * (1m - (request.DiscountPercent / 100m));
        var endDate = account.ContractExpiresAtUtc.AddYears(request.LockYears > 0 ? request.LockYears : 1);

        var agreement = new RenewalAgreement(
            AgreementId: agreementId,
            AccountId: account.AccountId,
            AnnualContractValue: discountedValue,
            DiscountAppliedPercent: request.DiscountPercent,
            NewContractEndDateUtc: endDate,
            PriceLockYears: request.LockYears,
            SignatureStatus: "PendingSignature",
            CreatedAtUtc: DateTime.UtcNow
        );

        _agreements[agreementId] = agreement;
        return Task.FromResult(agreement);
    }

    public Task<RescueMetricsSummary> GetRescueMetricsAsync(CancellationToken ct = default)
    {
        var assessments = _accounts.Values.Select(CalculateRiskAssessment).ToList();
        var arrAtRisk = assessments.Where(a => a.ChurnPropensityScore >= 60).Sum(a => a.AnnualRecurringRevenue);
        var highRiskCount = assessments.Count(a => a.ChurnPropensityScore >= 60);

        var metrics = new RescueMetricsSummary(
            TotalArrAtRisk: arrAtRisk,
            RescuedArrAmount: 339000m,
            HighRiskAccountsCount: highRiskCount,
            NetRevenueRetentionRate: 114.6,
            RescuedContractsCount: 14
        );

        return Task.FromResult(metrics);
    }

    private static ChurnRiskAssessment CalculateRiskAssessment(AccountHealthSignal acc)
    {
        var rawScore = (acc.UsageDropPercentage * 0.4)
            + ((100 - acc.LoginFrequencyScore) * 0.3)
            + ((1.0 - acc.SupportTicketSentiment) * 20)
            + (acc.ChampionTurnoverFlag ? 15 : 0);

        var propensityScore = (int)Math.Clamp(Math.Round(rawScore), 0, 100);

        var riskLevel = propensityScore switch
        {
            >= 80 => "SevereCritical",
            >= 60 => "High",
            >= 40 => "Moderate",
            _ => "Low"
        };

        var drivers = new List<string>();
        if (acc.UsageDropPercentage > 25) drivers.Add($"Usage declined {acc.UsageDropPercentage:F1}% in last 30d");
        if (acc.LoginFrequencyScore < 50) drivers.Add("User active session frequency severely reduced");
        if (acc.SupportTicketSentiment < 0.5) drivers.Add("Negative ticket sentiment trend detected");
        if (acc.ChampionTurnoverFlag) drivers.Add("Primary executive champion changed roles");

        if (drivers.Count == 0) drivers.Add("Stable recurring usage patterns");

        var daysUntilRenewal = (int)Math.Max(0, (acc.ContractExpiresAtUtc - DateTime.UtcNow).TotalDays);

        var playbook = propensityScore >= 75 ? "DiscountLock" : (propensityScore >= 50 ? "ExecutiveSponsorSync" : "UsageCredits");

        return new ChurnRiskAssessment(
            AccountId: acc.AccountId,
            AccountName: acc.AccountName,
            AnnualRecurringRevenue: acc.AnnualRecurringRevenue,
            ChurnPropensityScore: propensityScore,
            RiskLevel: riskLevel,
            PrimaryRiskDrivers: drivers,
            DaysUntilRenewal: daysUntilRenewal,
            RecommendedPlaybook: playbook
        );
    }
}
