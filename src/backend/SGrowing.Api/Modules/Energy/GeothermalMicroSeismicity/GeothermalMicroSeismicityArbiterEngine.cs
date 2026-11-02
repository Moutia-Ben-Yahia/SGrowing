namespace SGrowing.Api.Modules.Energy.GeothermalMicroSeismicity;

public class GeothermalMicroSeismicityArbiterEngine
{
    public GeothermalSeismicAssessment EvaluateReservoirSeismicity(GeothermalSeismicAnalysisRequest request)
    {
        if (request.RecentEvents.Count == 0)
        {
            var defaultCommands = request.Wells.Select(w => new WellControlCommand(
                WellId: w.WellId,
                TrafficLightStatus: "Green",
                RecommendedFlowRateLitersPerSec: w.CurrentInjectionRateLitersPerSec,
                PressureAdjustmentBar: 0.0,
                ActionReason: "Zero micro-seismic activity recorded; nominal injection schedule."
            )).ToList();

            string resName = request.Wells.FirstOrDefault()?.ReservoirName ?? "UnknownReservoir";
            return new GeothermalSeismicAssessment(
                ReservoirName: resName,
                TotalEventsAudited: 0,
                MaxMagnitudeRecorded: 0.0,
                GutenbergRichterBValue: 1.0,
                Commands: defaultCommands,
                ReservoirSafetyPosture: "Safe: Green TLS status. Hydraulic fracturing within geomechanical stability envelope."
            );
        }

        double maxMag = request.RecentEvents.Max(e => e.MagnitudeLocal);
        double minMag = request.RecentEvents.Min(e => e.MagnitudeLocal);
        double meanMag = request.RecentEvents.Average(e => e.MagnitudeLocal);

        // Maximum-likelihood Gutenberg-Richter b-value: b = log10(e) / (meanM - minM + 0.05)
        double diff = Math.Max(0.05, meanMag - minMag + 0.05);
        double bValue = Math.Round(Math.Log10(Math.E) / diff, 2);

        string trafficLight;
        string posture;

        if (maxMag >= request.RedThresholdMagnitude)
        {
            trafficLight = "Red";
            posture = $"CRITICAL HAZARD: Event magnitude {maxMag:F2} breached Red threshold ({request.RedThresholdMagnitude:F2}). Immediate well shut-in initiated.";
        }
        else if (maxMag >= request.AmberThresholdMagnitude || bValue < 0.80)
        {
            trafficLight = "Amber";
            posture = $"ELEVATED STRAIN: Magnitude {maxMag:F2} or low b-value ({bValue:F2}) indicates differential shear accumulation. 50% flow curtailment ordered.";
        }
        else
        {
            trafficLight = "Green";
            posture = $"OPERATIONAL STABILITY: Max magnitude {maxMag:F2} below Amber trigger ({request.AmberThresholdMagnitude:F2}). Normal hydraulic stimulation active.";
        }

        var commands = new List<WellControlCommand>();
        foreach (var well in request.Wells)
        {
            double recommendedFlow;
            double pressureAdjustment;
            string reason;

            switch (trafficLight)
            {
                case "Red":
                    recommendedFlow = 0.0;
                    pressureAdjustment = -Math.Round(well.WellheadPressureBar * 0.50, 1);
                    reason = "Emergency shut-in: stop pumps and bleed off wellhead pressure to arrest fault slip.";
                    break;
                case "Amber":
                    recommendedFlow = Math.Round(well.CurrentInjectionRateLitersPerSec * 0.50, 1);
                    pressureAdjustment = -Math.Round(well.WellheadPressureBar * 0.20, 1);
                    reason = "Precautionary throttling: cut injection rate by 50% to diffuse pore pressure gradient.";
                    break;
                default:
                    recommendedFlow = well.CurrentInjectionRateLitersPerSec;
                    pressureAdjustment = 0.0;
                    reason = "Nominal operation: baseline flow rate permitted.";
                    break;
            }

            commands.Add(new WellControlCommand(
                WellId: well.WellId,
                TrafficLightStatus: trafficLight,
                RecommendedFlowRateLitersPerSec: recommendedFlow,
                PressureAdjustmentBar: pressureAdjustment,
                ActionReason: reason
            ));
        }

        string reservoir = request.Wells.FirstOrDefault()?.ReservoirName ?? "GeothermalReservoir";
        return new GeothermalSeismicAssessment(
            ReservoirName: reservoir,
            TotalEventsAudited: request.RecentEvents.Count,
            MaxMagnitudeRecorded: Math.Round(maxMag, 2),
            GutenbergRichterBValue: bValue,
            Commands: commands,
            ReservoirSafetyPosture: posture
        );
    }
}
