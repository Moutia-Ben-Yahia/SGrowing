using System;

namespace SGrowing.Api.Modules.Defense.AdaptiveOpticsPhaseConjugation;

public sealed record AtmosphericAdaptiveOpticsTelemetry(
    string LaserSystemId,
    double TargetRangeKilometers,
    double LaserWavelengthNanometers,
    double RefractiveIndexStructureConstantCn2,
    double CrosswindVelocityMPerSec,
    double DeformableMirrorActuatorSpacingMeters,
    double ControlLoopBandwidthHz,
    double MeasuredRytovVariance,
    double BeamDirectorApertureDiameterMeters);

public sealed record AdaptiveOpticsAnalysisResult(
    string LaserSystemId,
    double FriedCoherenceLengthMeters,
    double GreenwoodFrequencyHz,
    double EstimatedRytovVariance,
    double TotalWavefrontVarianceRad2,
    double EstimatedStrehlRatio,
    string BeamPropagationRegime,
    string AdaptiveOpticsCommand,
    bool IsFullPowerAuthorized,
    DateTimeOffset Timestamp);
