namespace SGrowing.Api.Modules.Energy.UhvCoronaIonization;

public interface IUhvCoronaIonizationEngine
{
    GridCoronaLossProfile EvaluateCoronaDischarge(CoronaEvaluationRequest request);
}

public class UhvCoronaIonizationEngine : IUhvCoronaIonizationEngine
{
    public GridCoronaLossProfile EvaluateCoronaDischarge(CoronaEvaluationRequest request)
    {
        var geom = request.Geometry;
        var w = request.Weather;

        // Relative air density delta = (0.386 * P_torr) / (273.15 + T_celsius)
        double delta = (0.386 * Math.Max(500.0, w.BarometricPressureTorr)) / (273.15 + w.AmbientTempCelsius);
        delta = Math.Clamp(delta, 0.70, 1.20);

        // Surface roughness / weather irregularity factor m
        double m;
        string weatherRegime;
        if (w.PrecipitationRateMmPerHour > 1.0)
        {
            m = 0.62; // Rain drops form Taylor cones, reducing dielectric breakdown
            weatherRegime = "HEAVY_PRECIPITATION";
        }
        else if (w.RelativeHumidityPercent >= 85.0)
        {
            m = 0.76;
            weatherRegime = "HUMID_FOG";
        }
        else
        {
            m = 0.88; // Weathered stranded conductor under fair weather
            weatherRegime = "FAIR_WEATHER";
        }

        // Peek's formula for Critical Disruptive Surface Gradient E0 in kV_rms / cm
        // E0 = m * 21.2 * delta * (1 + 0.301 / sqrt(delta * r))
        double r = Math.Max(0.5, geom.SubconductorRadiusCm);
        double e0 = m * 21.2 * delta * (1.0 + (0.301 / Math.Sqrt(delta * r)));

        // Operating surface electric field gradient for bundle conductor:
        // E_op ~ (V_phase / (n * r)) * (1 + (n - 1) * r / R_bundle) / ln(D / r_eq)
        // For standard 1100kV AC 8-bundle: E_op is typically ~ 14.5 to 16.5 kV/cm
        double bundleRadius = geom.BundleSpacingCm / (2.0 * Math.Sin(Math.PI / Math.Max(2, geom.SubconductorsPerBundle)));
        double operatingGradient = (geom.LineVoltageKv / 1.732) * (1.0 + (geom.SubconductorsPerBundle - 1) * (r / bundleRadius)) / (geom.SubconductorsPerBundle * r * 4.2);

        // Corona Power Loss in kW / km (Peterson empirical formulation)
        double excessGradientRatio = Math.Max(0.0, (operatingGradient - e0) / e0);
        double baseLossKwPerKm = weatherRegime switch
        {
            "HEAVY_PRECIPITATION" => 120.0 + (excessGradientRatio * 250.0),
            "HUMID_FOG" => 35.0 + (excessGradientRatio * 90.0),
            _ => 3.5 + (excessGradientRatio * 25.0)
        };

        double lineKm = Math.Max(1.0, geom.LineLengthKm);
        double totalLossMw = (baseLossKwPerKm * lineKm) / 1000.0;

        // Audible noise in dBA at edge of right-of-way (~50m from outer phase)
        double audibleNoiseDba = 30.0 + (Math.Log10(Math.Max(1.0, baseLossKwPerKm)) * 14.5);

        string advisory;
        if (weatherRegime == "HEAVY_PRECIPITATION" && totalLossMw >= 50.0)
        {
            advisory = $"CORONA_LOSS_CRITICAL: Total ionization loss {totalLossMw:F1} MW across {lineKm:F0} km. Command substation on-load tap changers to trim bus voltage by -4.0% (down to 1055 kV) and energize shunt reactors.";
        }
        else if (weatherRegime == "HUMID_FOG")
        {
            advisory = $"CORONA_ELEVATED: Fog condensation active. Audible noise {audibleNoiseDba:F1} dBA. Active telemetry monitoring.";
        }
        else
        {
            advisory = $"NOMINAL_GRID_TRANSMISSION: Fair weather corona dissipation {totalLossMw:F1} MW (< 1% total capacity). Operating gradient within dielectric margin.";
        }

        return new GridCoronaLossProfile(
            TransmissionLineId: geom.TransmissionLineId,
            RelativeAirDensityDelta: Math.Round(delta, 3),
            CriticalDisruptiveGradientKvPerCm: Math.Round(e0, 2),
            OperatingGradientKvPerCm: Math.Round(operatingGradient, 2),
            CoronaPowerLossKwPerKm: Math.Round(baseLossKwPerKm, 2),
            TotalTransmissionLossMw: Math.Round(totalLossMw, 2),
            AudibleNoiseDba: Math.Round(audibleNoiseDba, 1),
            WeatherRegime: weatherRegime,
            GridAdvisory: advisory
        );
    }
}
