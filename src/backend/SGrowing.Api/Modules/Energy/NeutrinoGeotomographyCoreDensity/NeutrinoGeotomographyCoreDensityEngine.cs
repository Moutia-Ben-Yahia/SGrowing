using System;

namespace SGrowing.Api.Modules.Energy.NeutrinoGeotomographyCoreDensity;

public sealed class NeutrinoGeotomographyCoreDensityEngine : INeutrinoGeotomographyCoreDensityEngine
{
    public NeutrinoTomographyAnalysisResult AnalyzePlanetaryInterior(NeutrinoTomographyTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        const double earthRadiusKm = 6371.0;
        var nadirRad = Math.Clamp(telemetry.EarthChordNadirAngleDegrees, 0.0, 89.0) * (Math.PI / 180.0);
        var chordLengthKm = 2.0 * earthRadiusKm * Math.Cos(nadirRad);

        // Optical depth tau = -ln(T)
        var transmittance = Math.Clamp(telemetry.MeasuredNeutrinoTransmittance, 0.001, 0.999);
        var opticalDepth = -Math.Log(transmittance);

        // Theoretical PREM expected transmittance based on chord geometry and energy
        var isCoreCrossing = telemetry.EarthChordNadirAngleDegrees < 33.0;
        var premExpectedAvgDensity = isCoreCrossing ? 9.8 : 4.6; // g/cm^3
        var energyFactor = Math.Pow(Math.Max(1.0, telemetry.NeutrinoEnergyTeV) / 10.0, 0.42);
        var theoreticalExpectedTransmittance = Math.Round(Math.Clamp(Math.Exp(-0.000010 * chordLengthKm * premExpectedAvgDensity * energyFactor), 0.001, 0.999), 3);

        // Inferred average density along the chord
        var inferredDensity = Math.Round(opticalDepth / (0.000010 * Math.Max(100.0, chordLengthKm) * energyFactor), 2);

        // Radiogenic heat production (TW) from geo-neutrino flux
        var radiogenicHeatTw = Math.Round(telemetry.MeasuredGeoAntineutrinoFluxTnu * 0.42, 2);

        // Core-Mantle Boundary (CMB) density jump ratio
        var densityJumpRatio = Math.Round(isCoreCrossing ? (inferredDensity / 5.5) : 1.0, 2);

        string regime;
        string interpretation;
        bool isConsistent;

        if (telemetry.InferredGeoReactorPowerTerawatts > 3.5)
        {
            regime = "GEO_REACTOR_POWER_SURGE_DETECTED";
            interpretation = "Geo-reactor antineutrino spectrum exceeds 3.5 TW global empirical upper limit. Possible deep-earth natural nuclear fission anomaly.";
            isConsistent = false;
        }
        else if (inferredDensity > 14.5)
        {
            regime = "HEAVY_ELEMENT_CORE_ANOMALY";
            interpretation = "Chord-averaged density exceeds iron-nickel PREM boundary (14.5 g/cm3). Indicates heavy actinide accumulation at the inner core boundary.";
            isConsistent = false;
        }
        else if (radiogenicHeatTw < 10.0 || telemetry.ThoriumToUraniumMassRatio < 2.2)
        {
            regime = "CRUSTAL_RADIOACTIVITY_DEPLETION";
            interpretation = "Geo-neutrino heat flux below 10 TW or anomalous Th/U ratio. Subducted oceanic lithosphere radiogenic depletion detected.";
            isConsistent = false;
        }
        else
        {
            regime = "PREM_CORE_MANTLE_CONSISTENT";
            interpretation = "Neutrino transmission attenuation and radiogenic flux match Preliminary Reference Earth Model (PREM). Core-mantle boundary resolved.";
            isConsistent = true;
        }

        return new NeutrinoTomographyAnalysisResult(
            TomographyArrayId: telemetry.TomographyArrayId,
            InferredAverageChordDensityGramsPerCm3: inferredDensity,
            TheoreticalExpectedTransmittance: theoreticalExpectedTransmittance,
            RadiogenicHeatProductionTerawatts: radiogenicHeatTw,
            CoreMantleBoundaryDensityJumpRatio: densityJumpRatio,
            PlanetaryGeophysicsRegime: regime,
            GeophysicalInterpretation: interpretation,
            IsPlanetaryModelConsistent: isConsistent
        );
    }
}
