namespace SGrowing.Api.Modules.Telecom.SubseaAcousticEarlyWarning;

public class SubseaAcousticEarlyWarningEngine
{
    public SubseaEarlyWarningReport AnalyzeReadings(SubseaAcousticAnalysisRequest request)
    {
        var segmentsMap = request.CableSegments.ToDictionary(s => s.SegmentId);
        var alerts = new List<CableThreatAlert>();
        double peakStrain = 0.0;
        int alertIndex = 1;

        var seismicEvents = request.ActiveSeismicEvents ?? [];

        foreach (var reading in request.AcousticReadings)
        {
            if (reading.StrainMicrostrains > peakStrain)
            {
                peakStrain = reading.StrainMicrostrains;
            }

            if (!segmentsMap.TryGetValue(reading.SegmentId, out var segment))
            {
                continue;
            }

            if (reading.StrainMicrostrains < request.AlertStrainThresholdMicrostrains)
            {
                continue;
            }

            string threatCategory;
            double riskScore;
            string mitigation;

            // Scenario 1: Low frequency (1 - 15 Hz) with active seismic event
            if (reading.FrequencyHz <= 15.0 && seismicEvents.Count > 0)
            {
                var maxMag = seismicEvents.Max(s => s.MagnitudeRichter);
                threatCategory = "SeismicFaultRupture";
                riskScore = Math.Min(1.0, Math.Round((reading.StrainMicrostrains / 300.0) * (maxMag / 7.0), 2));
                mitigation = "Activate terrestrial optical mesh bypass; prep subsea repair vessel standby.";
            }
            // Scenario 2: High strain spike in shallow water (< 200m depth) -> Anchor drag
            else if (segment.WaterDepthMeters <= 200.0 && reading.StrainMicrostrains >= 200.0)
            {
                threatCategory = "AnchorDragImpact";
                riskScore = Math.Min(1.0, Math.Round(reading.StrainMicrostrains / 250.0, 2));
                mitigation = "Broadcast AIS hazard alert to coastal maritime guard; switch optical wavelengths.";
            }
            // Scenario 3: Deep water (> 1000m) with acoustic energy spike -> Subsea landslide/turbidity current
            else if (segment.WaterDepthMeters >= 1000.0 && reading.AcousticEnergyJoules >= 50.0)
            {
                threatCategory = "SubseaLandslide";
                riskScore = Math.Min(1.0, Math.Round(reading.StrainMicrostrains / 280.0, 2));
                mitigation = "Trigger automatic optical protection switching (APS) <50ms.";
            }
            else
            {
                threatCategory = "TrawlerNetSnag";
                riskScore = Math.Min(1.0, Math.Round(reading.StrainMicrostrains / 400.0, 2));
                mitigation = "Monitor strain gradient trend; log AIS maritime vessel correlation.";
            }

            alerts.Add(new CableThreatAlert(
                AlertId: $"ALERT-DAS-{alertIndex++:D3}",
                SegmentId: reading.SegmentId,
                PositionKm: reading.PositionKm,
                ThreatCategory: threatCategory,
                RiskScore: riskScore,
                RecommendedMitigation: mitigation
            ));
        }

        bool rerouteRequired = peakStrain >= 250.0 || alerts.Any(a => a.RiskScore >= 0.80);

        string healthStatus;
        if (alerts.Count == 0)
        {
            healthStatus = "Nominal: Distributed acoustic strain within safe oceanic envelope (<150 microstrains).";
        }
        else if (rerouteRequired)
        {
            healthStatus = "Critical: Cable integrity compromised; automated DWDM optical restoration active.";
        }
        else
        {
            healthStatus = "Elevated Alert: Anisotropic strain detected; precautionary surveillance enabled.";
        }

        return new SubseaEarlyWarningReport(
            TotalSegmentsMonitored: request.CableSegments.Count,
            TotalReadingsAnalyzed: request.AcousticReadings.Count,
            ActiveThreats: alerts,
            PeakStrainObserved: Math.Round(peakStrain, 2),
            OpticalRerouteRequired: rerouteRequired,
            NetworkHealthStatus: healthStatus
        );
    }
}
