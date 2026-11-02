using System;

namespace SGrowing.Api.Modules.Telecom.DirectToCellDopplerTimingAdvance;

public sealed record DirectToCellTelemetry(
    string SatelliteId,
    string UserTerminalId,
    double CarrierFrequencyHz,
    double SatelliteOrbitalVelocityMPerSec,
    double ElevationAngleDegrees,
    double SlantRangeKm,
    double PreCompensatedFrequencyShiftHz,
    double TimingDriftRateNsPerSec,
    double MeasuredPrachResidualDopplerHz);

public sealed record DirectToCellAnalysisResult(
    string SatelliteId,
    string UserTerminalId,
    double DopplerShiftHz,
    double DopplerRateHzPerSec,
    double PropagationDelayMicroseconds,
    double TimingAdvanceMicroseconds,
    double ResidualDopplerErrorHz,
    string NtnLinkQualityStatus,
    string RecommendedSynchronizationAction,
    bool IsHandoverImminent,
    DateTimeOffset Timestamp);
