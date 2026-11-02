namespace SGrowing.Api.Modules.Manufacturing.CleanroomAirflowPredictor;

public class CleanroomAirflowPredictorEngine
{
    private const double FfuMotorNominalPowerKw = 0.25; // 250W ECM motor per FFU

    public CleanroomFacilityReport EvaluateCleanroomFacility(CleanroomAnalysisRequest request)
    {
        var sensorMap = request.SensorReadings.ToDictionary(s => s.ZoneId);
        var audits = new List<ZoneCleanlinessAudit>();
        bool allCompliant = true;
        double totalPowerKw = 0.0;

        foreach (var zone in request.Zones)
        {
            sensorMap.TryGetValue(zone.ZoneId, out var sensor);

            double p01 = sensor?.ParticlesPerM3Gte01Um ?? 50.0;
            double p05 = sensor?.ParticlesPerM3Gte05Um ?? 2.0;
            double velocity = sensor?.AirVelocityMps ?? zone.NominalAirVelocityMps;

            // ISO 14644-1 classification limits at 0.5 um
            double limit05 = zone.TargetIsoClass switch
            {
                1 => 0.0,
                2 => 4.0,
                3 => 35.0,
                4 => 352.0,
                5 => 3520.0,
                _ => 10000.0
            };

            int achievedClass;
            if (p01 <= 10 && p05 == 0) achievedClass = 1;
            else if (p01 <= 100 && p05 <= 4) achievedClass = 2;
            else if (p01 <= 1000 && p05 <= 35) achievedClass = 3;
            else if (p01 <= 10000 && p05 <= 352) achievedClass = 4;
            else if (p01 <= 100000 && p05 <= 3520) achievedClass = 5;
            else achievedClass = 6;

            bool inSpec = achievedClass <= zone.TargetIsoClass;
            if (!inSpec) allCompliant = false;

            // Simplified Air Changes Per Hour (ACH): ACH = (Velocity * FilterCoverageArea * 3600) / Volume
            // Typical cleanroom filter coverage ~ 60% of ceiling
            double ceilingAreaM2 = zone.VolumeCubicMeters / 3.0; // Assume 3m ceiling height
            double filterAreaM2 = ceilingAreaM2 * 0.60;
            double ach = Math.Round((velocity * filterAreaM2 * 3600.0) / zone.VolumeCubicMeters, 1);

            double speedAdjustment = 0.0;
            string recommendation;

            if (!inSpec || p05 > limit05 * 0.80)
            {
                speedAdjustment = 15.0; // Boost FFU speed to flush particulate load
                recommendation = $"Contamination Threat: Particulate count ({p05}/m³) near threshold ({limit05}/m³). Ramp FFU speed by +15% and check airlock seal.";
            }
            else if (p05 < limit05 * 0.25 && velocity > 0.40)
            {
                speedAdjustment = -10.0; // Trim FFU speed to save power
                recommendation = $"Eco-Optimized: Particulate count is well within safety margin. Trim FFU fan speed by -10% to reduce energy draw.";
            }
            else
            {
                recommendation = "Laminar Stability: Uniform laminar downdraft maintained; FFU setpoint nominal.";
            }

            double ffuPower = zone.FfuCount * FfuMotorNominalPowerKw * (1.0 + (speedAdjustment / 100.0));
            totalPowerKw += ffuPower;

            audits.Add(new ZoneCleanlinessAudit(
                ZoneId: zone.ZoneId,
                BayName: zone.BayName,
                TargetIsoClass: zone.TargetIsoClass,
                AchievedIsoClass: achievedClass,
                ParticleCount05Um: p05,
                AirChangesPerHour: ach,
                FfuSpeedAdjustmentPercent: speedAdjustment,
                InSpec: inSpec,
                OptimizationRecommendation: recommendation
            ));
        }

        double dailyKwh = totalPowerKw * 24.0;
        decimal dailyCost = Math.Round((decimal)(dailyKwh * request.ElectricityCostEurPerKwh), 2);

        string quality = allCompliant
            ? "Facility Compliant: All photolithography and wafer bays operate within certified ISO 14644 boundaries."
            : "Airflow Out-Of-Spec: One or more semiconductor fabrication bays require immediate laminar air flushing.";

        return new CleanroomFacilityReport(
            TotalBaysAudited: request.Zones.Count,
            AllBaysCompliant: allCompliant,
            TotalFfuPowerConsumptionKw: Math.Round(totalPowerKw, 2),
            DailyEnergyCostEur: dailyCost,
            BayAudits: audits,
            OverallAirflowQuality: quality
        );
    }
}
