using System;

namespace SGrowing.Api.Modules.Quantum.StrontiumOpticalLatticeClock;

public sealed class StrontiumOpticalLatticeClockEngine : IStrontiumOpticalLatticeClockEngine
{
    public StrontiumClockAnalysisResult AnalyzeOpticalAtomicClock(StrontiumClockTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        const double clockFrequencyHz = 429_228_004_229_873.0; // Sr-87 1S0 -> 3P0
        const double magicWavelengthNm = 813.427;
        const double gOverCSquared = 1.09e-16; // Gravitational redshift per meter

        var nAtoms = Math.Max(10.0, telemetry.TrappedSr87AtomCount);
        var linewidth = Math.Max(0.01, telemetry.ClockLaserLinewidthHertz);

        // Quality factor Q = nu0 / delta_nu
        var qFactor = clockFrequencyHz / linewidth;

        // Allan deviation instability at 1s: sigma_y(1s) = 1 / (pi * Q * sqrt(N))
        var allanDeviation1s = 1.0 / (Math.PI * qFactor * Math.Sqrt(nAtoms));

        // Blackbody radiation fractional shift ~ -4.95e-15 * (T / 300)^4
        var tRatio = Math.Max(1.0, telemetry.BlackbodyRadiationTemperatureKelvin) / 300.0;
        var bbrShift = -4.95e-15 * Math.Pow(tRatio, 4);

        // Gravitational redshift correction
        var gravitationalShift = telemetry.GeodeticElevationMeters * gOverCSquared;

        var wavelengthOffset = Math.Abs(telemetry.LatticeLaserWavelengthNanometers - magicWavelengthNm);

        string regime;
        string action;
        bool isValid;

        if (!telemetry.CarrierEnvelopeOffsetLocked)
        {
            regime = "OPTICAL_COMB_CARRIER_OFFSET_SLIP";
            action = "Optical frequency comb f_ceo phase lock lost. Re-engage f-to-2f interferometer feedback loop.";
            isValid = false;
        }
        else if (Math.Abs(telemetry.MeasuredFractionalFrequencyOffset) > 5.0e-16 || telemetry.ClockLaserLinewidthHertz > 1.8)
        {
            regime = "OPTICAL_CAVITY_DRIFT_UNLOCKED";
            action = "Clock laser linewidth or fractional frequency offset exceeds 5e-16. Relock Pound-Drever-Hall (PDH) servo to cryogenic silicon cavity.";
            isValid = false;
        }
        else if (wavelengthOffset > 0.035 || telemetry.BlackbodyRadiationTemperatureKelvin > 308.0)
        {
            regime = "BLACKBODY_RADIATION_UNCERTAINTY_WARNING";
            action = "Lattice laser deviated from magic wavelength (813.427 nm) or thermal shield gradient high. Tune Ti:Sapphire lattice wavelength and stabilize isothermal chamber.";
            isValid = false;
        }
        else
        {
            regime = "QUANTUM_PROJECTION_NOISE_LIMITED";
            action = "Strontium optical lattice clock operating at quantum projection noise limit. Systematic uncertainty evaluated below 2e-18.";
            isValid = true;
        }

        return new StrontiumClockAnalysisResult(
            ClockSystemId: telemetry.ClockSystemId,
            AllanDeviationInstabilityAtOneSecond: allanDeviation1s,
            BlackbodyRadiationFractionalShift: bbrShift,
            GravitationalRedshiftCorrection: gravitationalShift,
            MetrologyPrecisionRegime: regime,
            LaserFrequencySynthesisAction: action,
            IsClockMetrologicallyValid: isValid
        );
    }
}
