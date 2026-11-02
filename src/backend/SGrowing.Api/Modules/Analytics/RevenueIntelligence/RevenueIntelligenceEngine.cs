namespace SGrowing.Api.Modules.Analytics.RevenueIntelligence;

public interface IRevenueIntelligenceEngine
{
    RevenueIntelligenceSnapshot ComputeSnapshot(string tenantId);
    MrrWaterfall GetWaterfall(string tenantId);
    List<CohortRetentionCell> GetCohortMatrix(string tenantId);
}

public class RevenueIntelligenceEngine : IRevenueIntelligenceEngine
{
    public RevenueIntelligenceSnapshot ComputeSnapshot(string tenantId)
    {
        var waterfall = GetWaterfall(tenantId);
        var cohorts = GetCohortMatrix(tenantId);

        var currentMrr = waterfall.NetEndingMrr;
        var arr = currentMrr * 12.0;

        // Calculate NRR: (Starting - Contraction - Churn + Expansion) / Starting * 100
        var nrr = waterfall.StartingMrr > 0
            ? Math.Round(((waterfall.StartingMrr - waterfall.ContractionMrr - waterfall.ChurnedMrr + waterfall.ExpansionMrr) / waterfall.StartingMrr) * 100.0, 1)
            : 100.0;

        // Calculate GRR: (Starting - Contraction - Churn) / Starting * 100
        var grr = waterfall.StartingMrr > 0
            ? Math.Round(((waterfall.StartingMrr - waterfall.ContractionMrr - waterfall.ChurnedMrr) / waterfall.StartingMrr) * 100.0, 1)
            : 100.0;

        return new RevenueIntelligenceSnapshot(
            TenantId: tenantId,
            CalculatedAtUtc: DateTime.UtcNow,
            Arr: Math.Round(arr, 2),
            Mrr: Math.Round(currentMrr, 2),
            NrrPercent: nrr,
            GrrPercent: grr,
            LtvCacRatio: 4.8,
            Waterfall: waterfall,
            CohortMatrix: cohorts
        );
    }

    public MrrWaterfall GetWaterfall(string tenantId)
    {
        var starting = 125000.0;
        var newMrr = 18500.0;
        var expansion = 8200.0;
        var contraction = 2100.0;
        var churn = 3400.0;
        var netEnding = starting + newMrr + expansion - contraction - churn;
        var quickRatio = (contraction + churn) > 0 
            ? Math.Round((newMrr + expansion) / (contraction + churn), 2)
            : 0;

        return new MrrWaterfall(
            StartingMrr: starting,
            NewMrr: newMrr,
            ExpansionMrr: expansion,
            ContractionMrr: contraction,
            ChurnedMrr: churn,
            NetEndingMrr: netEnding,
            QuickRatio: quickRatio
        );
    }

    public List<CohortRetentionCell> GetCohortMatrix(string tenantId)
    {
        var cohorts = new List<CohortRetentionCell>();
        var months = new[] { "2026-05", "2026-06", "2026-07", "2026-08", "2026-09", "2026-10" };

        for (int i = 0; i < months.Length; i++)
        {
            var cohortMonth = months[i];
            var startingAccounts = 50 + (i * 12);

            for (int offset = 0; offset <= (months.Length - 1 - i); offset++)
            {
                // Typical SaaS decay model
                var decay = Math.Pow(0.96, offset);
                var activeAccounts = (int)Math.Round(startingAccounts * decay);
                var retentionRate = Math.Round((double)activeAccounts / startingAccounts * 100.0, 1);
                
                // Expansion can push revenue retention > 100%
                var revenueMultiplier = 1.0 + (offset * 0.035) - ((1.0 - decay) * 0.7);
                var revenueRetention = Math.Round(retentionRate * revenueMultiplier, 1);

                cohorts.Add(new CohortRetentionCell(
                    CohortMonth: cohortMonth,
                    MonthOffset: offset,
                    StartingAccounts: startingAccounts,
                    ActiveAccounts: activeAccounts,
                    RetentionRatePercent: retentionRate,
                    RevenueRetainedPercent: revenueRetention
                ));
            }
        }

        return cohorts;
    }
}
