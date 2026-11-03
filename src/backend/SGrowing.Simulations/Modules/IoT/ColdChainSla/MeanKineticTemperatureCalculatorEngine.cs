namespace SGrowing.Api.Modules.IoT.ColdChainSla;

/// <summary>
/// Autonomous FDA / USP <1079> Mean Kinetic Temperature (MKT) Arrhenius engine
/// evaluating thermal degradation kinetics and sensor excursions for cold-chain shipments.
/// </summary>
public sealed class MeanKineticTemperatureCalculatorEngine
{
    // Standard Arrhenius activation energy for pharmaceutical / biological degradation:
    // DeltaH / R = 83.144 kJ/mol / 8.3144 J/(mol K) = 10,000.0 Kelvin
    public const double DeltaHOverR = 10000.0;

    public (double MktCelsius, double MinTemp, double MaxTemp, int ExcursionMinutes, double MaxShockG) CalculateMkt(
        IReadOnlyList<TemperatureSensorSample> samples,
        double minAllowedTemp,
        double maxAllowedTemp,
        int sampleIntervalMinutes = 10)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) return (0.0, 0.0, 0.0, 0, 0.0);

        var minTemp = double.MaxValue;
        var maxTemp = double.MinValue;
        var maxShock = 0.0;
        var excursionCount = 0;
        var sumExp = 0.0;

        foreach (var sample in samples)
        {
            if (sample.TemperatureCelsius < minTemp) minTemp = sample.TemperatureCelsius;
            if (sample.TemperatureCelsius > maxTemp) maxTemp = sample.TemperatureCelsius;
            if (sample.ShockGForce > maxShock) maxShock = sample.ShockGForce;

            // Check if sample breached bounds
            if (sample.TemperatureCelsius < minAllowedTemp || sample.TemperatureCelsius > maxAllowedTemp)
            {
                excursionCount++;
            }

            // Convert Celsius to Kelvin
            var tk = sample.TemperatureCelsius + 273.15;
            if (tk <= 0.0) tk = 0.1;

            sumExp += Math.Exp(-DeltaHOverR / tk);
        }

        var avgExp = sumExp / samples.Count;
        // MKT = (DeltaH / R) / -ln(avgExp) - 273.15
        var mktKelvin = DeltaHOverR / (-Math.Log(avgExp));
        var mktCelsius = mktKelvin - 273.15;

        var totalExcursionMinutes = excursionCount * sampleIntervalMinutes;

        return (
            Math.Round(mktCelsius, 2),
            Math.Round(minTemp, 2),
            Math.Round(maxTemp, 2),
            totalExcursionMinutes,
            Math.Round(maxShock, 2)
        );
    }
}
