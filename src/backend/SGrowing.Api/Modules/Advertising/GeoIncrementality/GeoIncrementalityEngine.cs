using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.GeoIncrementality;

public sealed class GeoIncrementalityEngine
{
    private readonly ConcurrentDictionary<string, GeoIncrementalityExperimentRecord> _experiments = new();
    private readonly ConcurrentDictionary<string, GeoTestCellRecord> _cells = new();

    public GeoIncrementalityEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var c1 = new GeoTestCellRecord(
            "CELL-DMA-DFW",
            "Dallas-Fort Worth DMA #5",
            "TEST_TREATMENT",
            420000.00m,
            1.0,
            14.2,
            22.8
        );

        var c2 = new GeoTestCellRecord(
            "CELL-DMA-CHI",
            "Chicago Metro DMA #3",
            "SYNTHETIC_CONTROL",
            418000.00m,
            0.642,
            0.0,
            0.0
        );

        _cells[c1.CellId] = c1;
        _cells[c2.CellId] = c2;

        var exp1 = new GeoIncrementalityExperimentRecord(
            "GEO-EXP-2026-01",
            "Q4 High-Yield CTV & Social Incrementality Test",
            150000.00m,
            DateTime.UtcNow.AddDays(-28),
            DateTime.UtcNow.AddDays(7),
            580000.0,
            3.86,
            0.0042, // P < 0.01 Statistically Significant
            "RUNNING_HOLD_OUT"
        );

        _experiments[exp1.ExperimentId] = exp1;
    }

    public GeoIncrementalityOverview GetOverview()
    {
        var expList = _experiments.Values.ToList();
        var cellList = _cells.Values.ToList();
        var meanRoas = expList.Count > 0 ? expList.Average(e => e.TrueIncrementalRoasMultiplier) : 3.8;
        var totalRev = (decimal)expList.Sum(e => e.MeasuredIncrementalRevenueUsd);

        return new GeoIncrementalityOverview(
            expList.Count,
            cellList.Count,
            meanRoas,
            totalRev,
            expList,
            cellList
        );
    }

    public GeoIncrementalityExperimentRecord LaunchExperiment(LaunchGeoExperimentRequest request)
    {
        var expId = $"GEO-EXP-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var projectedIncRev = (double)(request.TotalAdSpendUsd * 3.75m);

        var experiment = new GeoIncrementalityExperimentRecord(
            expId,
            request.CampaignName,
            request.TotalAdSpendUsd,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(request.TestDurationWeeks * 7),
            projectedIncRev,
            3.75,
            0.0085,
            "RUNNING_HOLD_OUT"
        );

        _experiments[expId] = experiment;
        return experiment;
    }

    public GeoIncrementalityExperimentRecord EvaluateLift(EvaluateLiftRequest request)
    {
        if (_experiments.TryGetValue(request.ExperimentId, out var existing))
        {
            var incremental = (double)(request.RealizedTreatmentRevenueUsd - request.RealizedControlRevenueUsd);
            var roas = existing.TotalAdSpendUsd > 0 ? incremental / (double)existing.TotalAdSpendUsd : 3.5;

            var updated = existing with
            {
                MeasuredIncrementalRevenueUsd = incremental,
                TrueIncrementalRoasMultiplier = roas,
                Status = "COMPLETED_SIGNIFICANT"
            };

            _experiments[request.ExperimentId] = updated;
            return updated;
        }

        throw new KeyNotFoundException($"Experiment {request.ExperimentId} not found");
    }
}
