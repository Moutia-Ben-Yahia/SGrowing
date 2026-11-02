using System;

namespace SGrowing.Api.Modules.Infrastructure.SeismicMetamaterialPhononicCloak;

public sealed class SeismicMetamaterialPhononicCloakEngine : ISeismicMetamaterialPhononicCloakEngine
{
    public SeismicCloakAnalysisResult AnalyzeSeismicCloaking(SeismicCloakTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var cR = Math.Max(50.0, telemetry.SoilRayleighWaveVelocityMetersPerSec);
        var latticeA = Math.Max(0.5, telemetry.PhononicLatticeConstantMeters);

        // Bragg scattering center frequency: f = cR / (2 * a)
        var braggCenterHz = cR / (2.0 * latticeA);

        var fPred = Math.Max(0.1, telemetry.PredominantSeismicFrequencyHz);
        var fLow = Math.Max(0.1, telemetry.ResonatorBandgapLowerBoundHz);
        var fHigh = Math.Max(fLow + 0.1, telemetry.ResonatorBandgapUpperBoundHz);
        var fMid = 0.5 * (fLow + fHigh);
        var fHalfSpan = 0.5 * (fHigh - fLow);

        var isInBandgap = fPred >= fLow && fPred <= fHigh;

        double baseAttenuationDb;
        if (isInBandgap)
        {
            var bandProximity = 1.0 - Math.Clamp(Math.Abs(fPred - fMid) / fHalfSpan, 0.0, 1.0);
            baseAttenuationDb = 12.0 + 8.0 * bandProximity;
        }
        else
        {
            baseAttenuationDb = 2.0;
        }

        // Soil liquefaction damping loss
        var liqRisk = Math.Clamp(telemetry.SoilLiquefactionRiskIndex, 0.0, 1.0);
        var netAttenuationDb = baseAttenuationDb * (1.0 - 0.75 * liqRisk);

        // Wave power deflection and transmitted acceleration
        var energyTransmittedRatio = Math.Pow(10.0, -netAttenuationDb / 10.0);
        var waveEnergyDeflection = Math.Clamp((1.0 - energyTransmittedRatio) * 100.0, 0.0, 99.5);

        var amplitudeAttenuationRatio = Math.Pow(10.0, -netAttenuationDb / 20.0);
        var protectedPga = telemetry.IncidentPeakGroundAccelerationG * amplitudeAttenuationRatio;

        string regime;
        string guidance;
        bool isEffective;

        if (telemetry.EarthquakeMomentMagnitudeMw > 7.5 && (protectedPga > 0.20 || telemetry.IncidentPeakGroundAccelerationG > 1.2))
        {
            regime = "SUPER_MAGNITUDE_EARTHQUAKE_OVERTOPPING";
            guidance = "Earthquake energy exceeds metamaterial scattering capacity (PGA > 0.38g). Engage base isolation dampers and emergency building evacuation alarms.";
            isEffective = false;
        }
        else if (!isInBandgap)
        {
            regime = "OUT_OF_BANDGAP_SEISMIC_PENETRATION";
            guidance = "Dominant ground motion frequency falls outside phononic bandgap window. Actuate piezo-tunable resonant mass scatterers to shift bandgap.";
            isEffective = false;
        }
        else if (liqRisk > 0.70)
        {
            regime = "SOIL_LIQUEFACTION_MATRIX_DECOUPLING";
            guidance = "Pore water pressure surge threatens soil shear modulus and metamaterial matrix coupling. Engage subterranean dewatering well points.";
            isEffective = false;
        }
        else
        {
            regime = "PHONONIC_BANDGAP_SEISMIC_WAVE_CLOAKING";
            guidance = "Surface Rayleigh and Love waves effectively diverted around foundation perimeter within phononic bandgap. Subterranean cloaking operational.";
            isEffective = true;
        }

        return new SeismicCloakAnalysisResult(
            SiteId: telemetry.SiteId,
            BraggScatteringCenterFrequencyHz: braggCenterHz,
            ProtectedStructurePeakGroundAccelerationG: protectedPga,
            GroundWaveAttenuationDecibels: netAttenuationDb,
            WaveEnergyDeflectionPercent: waveEnergyDeflection,
            CloakingRegime: regime,
            GeotechnicalActionGuidance: guidance,
            IsCloakingEffective: isEffective
        );
    }
}
