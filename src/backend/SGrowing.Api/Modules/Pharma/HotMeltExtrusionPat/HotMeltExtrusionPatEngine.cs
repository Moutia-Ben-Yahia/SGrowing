namespace SGrowing.Api.Modules.Pharma.HotMeltExtrusionPat;

public class HotMeltExtrusionPatEngine
{
    private const double MaxAllowedCrystallinity = 0.05; // 5% max for amorphous solid dispersion

    public HmeBatchMonitoringResult EvaluateExtrusionStream(HmeBatchMonitoringRequest request)
    {
        var nir = request.NirReading;
        var zones = request.BarrelZones;

        double meanTemp = zones.Count > 0 ? Math.Round(zones.Average(z => z.BarrelTemperatureCelsius), 1) : request.DieTemperatureCelsius;

        // 1. API Concentration tolerance
        double apiDeviation = Math.Abs(nir.ApiConcentrationWeightPercent - nir.TargetApiPercent);
        bool apiInSpec = apiDeviation <= nir.TolerancePercent;

        // 2. Solid-state crystallinity
        bool amorphousStateMaintained = nir.CrystallinityIndex <= MaxAllowedCrystallinity;

        // 3. Barrel thermal equilibrium
        bool thermalStability = zones.All(z => Math.Abs(z.BarrelTemperatureCelsius - z.TargetTemperatureCelsius) <= 4.0);

        // 4. Melt pressure stability
        bool pressureNormal = request.MeltPressureBar is >= 20.0 and <= 180.0;

        bool inControl = apiInSpec && amorphousStateMaintained && thermalStability && pressureNormal;

        bool divertActuated = !inControl;
        string divertReason;
        string correctiveAction;

        if (!apiInSpec)
        {
            divertReason = $"API Concentration OOS ({nir.ApiConcentrationWeightPercent:F2}% vs target {nir.TargetApiPercent:F2}% ± {nir.TolerancePercent:F2}%).";
            correctiveAction = "Actuate high-speed pneumatic divert valve; trim gravimetric feeder screw rate.";
        }
        else if (!amorphousStateMaintained)
        {
            divertReason = $"Recrystallization Risk: Crystallinity index ({nir.CrystallinityIndex:P1} > {MaxAllowedCrystallinity:P1}). Incomplete API dissolution.";
            correctiveAction = "Divert extrudate to scrap bin; increase kneading block shear and die temperature.";
        }
        else if (!thermalStability)
        {
            divertReason = "Barrel Thermal Drift: Temperature deviates > 4.0°C from validated heating profile.";
            correctiveAction = "Divert stream; engage closed-loop oil cooling jacket recirculator.";
        }
        else if (!pressureNormal)
        {
            divertReason = $"Abnormal Melt Pressure ({request.MeltPressureBar} bar). Possible die obstruction or screw starvation.";
            correctiveAction = "Actuate divert gate; inspect screen pack differential pressure.";
        }
        else
        {
            divertReason = "None: Continuous process operating in verified design space.";
            correctiveAction = "Maintain steady-state extrusion to downstream pelletizer.";
        }

        string releaseVerdict = inControl
            ? "GMP In-Spec: Continuous amorphous solid dispersion validated. Real-time release testing (RTRT) approved."
            : $"GMP Out-of-Spec: Quality defect detected. Extrudate diverted to quarantine; batch deviation ticket logged.";

        return new HmeBatchMonitoringResult(
            BatchId: request.BatchId,
            ProcessInStatisticalControl: inControl,
            MeanBarrelTemperatureCelsius: meanTemp,
            ApiPurityConcentrationPercent: nir.ApiConcentrationWeightPercent,
            DivertStatus: new HmeDivertAction(divertActuated, divertReason, correctiveAction),
            QualityReleaseVerdict: releaseVerdict
        );
    }
}
