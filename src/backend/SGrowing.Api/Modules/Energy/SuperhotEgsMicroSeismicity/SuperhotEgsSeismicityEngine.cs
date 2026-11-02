namespace SGrowing.Api.Modules.Energy.SuperhotEgsMicroSeismicity;

public interface ISuperhotEgsSeismicityEngine
{
    EgsSeismicityMitigationPlan EvaluateSeismicity(SuperhotEgsEvaluationRequest request);
}

public class SuperhotEgsSeismicityEngine : ISuperhotEgsSeismicityEngine
{
    public EgsSeismicityMitigationPlan EvaluateSeismicity(SuperhotEgsEvaluationRequest request)
    {
        var tel = request.Telemetry;
        var thresh = request.Thresholds;
        var events = tel.RecentSeismicEvents ?? [];

        int totalEvents = events.Count;
        double maxMagnitude = 0.0;
        double maxPgv = 0.0;
        double sumMagnitude = 0.0;

        foreach (var ev in events)
        {
            if (ev.MagnitudeLocal > maxMagnitude) maxMagnitude = ev.MagnitudeLocal;
            if (ev.PeakGroundVelocityMmPerSec > maxPgv) maxPgv = ev.PeakGroundVelocityMmPerSec;
            sumMagnitude += ev.MagnitudeLocal;
        }

        // Gutenberg-Richter b-value estimation via Aki maximum likelihood
        double meanMagnitude = totalEvents > 0 ? (sumMagnitude / totalEvents) : 0.5;
        double bValue = totalEvents > 3 ? (Math.Log10(Math.E) / Math.Max(0.2, meanMagnitude - 0.1)) : 1.05;
        bValue = Math.Clamp(bValue, 0.65, 2.2);

        // Regulatory Traffic Light System (TLS) Classification
        bool redTrigger = maxMagnitude >= thresh.RedMagnitudeThreshold || maxPgv >= thresh.MaxAllowablePgvMmPerSec;
        bool amberTrigger = maxMagnitude >= thresh.AmberMagnitudeThreshold && !redTrigger;

        string tlsStatus;
        double recFlowRate;
        double recPressure;
        string directive;

        if (redTrigger)
        {
            tlsStatus = "RED";
            recFlowRate = 0.0;
            recPressure = Math.Max(0.0, tel.WellheadPressureBar * 0.40);
            directive = $"EMERGENCY_SHUT_IN: Red light triggered (Max M = {maxMagnitude:F1}, PGV = {maxPgv:F2} mm/s). Cease hydraulic stimulation immediately, open bleed-off valve to reduce wellhead pressure to {recPressure:F0} bar, and monitor focal mechanism focal shifts.";
        }
        else if (amberTrigger)
        {
            tlsStatus = "AMBER";
            recFlowRate = Math.Max(5.0, tel.InjectionFlowRateLitersPerSec * 0.50);
            recPressure = tel.WellheadPressureBar * 0.80;
            directive = $"THROTTLE_INJECTION: Amber light (1.0 <= M < 2.0). Halve injection rate to {recFlowRate:F1} L/s and ease wellhead pressure to {recPressure:F0} bar to halt fracture runway propagation.";
        }
        else
        {
            tlsStatus = "GREEN";
            recFlowRate = tel.InjectionFlowRateLitersPerSec;
            recPressure = tel.WellheadPressureBar;
            directive = $"CONTINUE_STIMULATION: Micro-seismic events within green baseline (Max M = {maxMagnitude:F1} < {thresh.AmberMagnitudeThreshold:F1}). Supercritical rock fracture permeability enhancing nominally.";
        }

        return new EgsSeismicityMitigationPlan(
            WellId: tel.WellId,
            TotalRecordedSeismicEvents: totalEvents,
            MaxObservedMagnitude: Math.Round(maxMagnitude, 2),
            GutenbergRichterBValue: Math.Round(bValue, 2),
            TrafficLightStatus: tlsStatus,
            RecommendedInjectionFlowRateLitersPerSec: Math.Round(recFlowRate, 1),
            RecommendedWellheadPressureBar: Math.Round(recPressure, 1),
            MitigationDirective: directive
        );
    }
}
