using System;

namespace SGrowing.Api.Modules.Telecom.DirectToCellDopplerTimingAdvance;

public interface IDirectToCellDopplerTimingAdvanceEngine
{
    DirectToCellAnalysisResult AnalyzeLinkSynchronization(DirectToCellTelemetry telemetry);
}

public sealed class DirectToCellDopplerTimingAdvanceEngine : IDirectToCellDopplerTimingAdvanceEngine
{
    private const double SpeedOfLightMPerSec = 299792458.0;

    public DirectToCellAnalysisResult AnalyzeLinkSynchronization(DirectToCellTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.SlantRangeKm <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Slant range must be strictly positive.");
        }

        double thetaRad = telemetry.ElevationAngleDegrees * (Math.PI / 180.0);
        double slantRangeMeters = telemetry.SlantRangeKm * 1000.0;

        // Doppler Shift: fd = fc * (v * cos(theta) / c)
        double dopplerShiftHz = telemetry.CarrierFrequencyHz * ((telemetry.SatelliteOrbitalVelocityMPerSec * Math.Cos(thetaRad)) / SpeedOfLightMPerSec);

        // Doppler Rate: df/dt = -(fc * v^2 * sin^2(theta)) / (c * R)
        double sinTheta = Math.Sin(thetaRad);
        double dopplerRateHzPerSec = -1.0 * (telemetry.CarrierFrequencyHz * Math.Pow(telemetry.SatelliteOrbitalVelocityMPerSec, 2.0) * Math.Pow(sinTheta, 2.0))
                                     / (SpeedOfLightMPerSec * slantRangeMeters);

        // One-way propagation delay and two-way Timing Advance
        double propagationDelaySeconds = slantRangeMeters / SpeedOfLightMPerSec;
        double propagationDelayMicroseconds = propagationDelaySeconds * 1_000_000.0;
        double timingAdvanceMicroseconds = 2.0 * propagationDelayMicroseconds;

        // Residual Doppler Error after current pre-compensation
        double residualDopplerErrorHz = Math.Abs(dopplerShiftHz - telemetry.PreCompensatedFrequencyShiftHz);
        if (Math.Abs(telemetry.MeasuredPrachResidualDopplerHz) > 0.0)
        {
            residualDopplerErrorHz = Math.Max(residualDopplerErrorHz, Math.Abs(telemetry.MeasuredPrachResidualDopplerHz));
        }

        string linkStatus;
        string action;
        bool isHandoverImminent = telemetry.ElevationAngleDegrees < 15.0;

        if (residualDopplerErrorHz > 1200.0 || Math.Abs(dopplerRateHzPerSec) > 500.0)
        {
            linkStatus = "EXCESSIVE_DOPPLER_MISMATCH";
            action = "APPLY_DOWNLINK_PRE_DOPPLER_COMPENSATION_AND_EXTEND_PRACH_FILTER";
        }
        else if (telemetry.TimingDriftRateNsPerSec > 30.0 || propagationDelayMicroseconds > 3500.0)
        {
            linkStatus = "TIMING_ADVANCE_DRIFT_ALERT";
            action = "DISPATCH_CONTINUOUS_MAC_CE_TIMING_ADVANCE_UPDATE";
        }
        else
        {
            linkStatus = "NOMINAL_NTN_LINK";
            action = "MAINTAIN_EPHEMERIS_GUIDED_BEAM_TRACKING";
        }

        return new DirectToCellAnalysisResult(
            telemetry.SatelliteId,
            telemetry.UserTerminalId,
            Math.Round(dopplerShiftHz, 2),
            Math.Round(dopplerRateHzPerSec, 2),
            Math.Round(propagationDelayMicroseconds, 3),
            Math.Round(timingAdvanceMicroseconds, 3),
            Math.Round(residualDopplerErrorHz, 2),
            linkStatus,
            action,
            isHandoverImminent,
            DateTimeOffset.UtcNow);
    }
}
