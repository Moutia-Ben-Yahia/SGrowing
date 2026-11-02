using System;

namespace SGrowing.Api.Modules.Energy.HempSubstationProtection;

public sealed record SubstationHempTelemetry(
    string SubstationId,
    double GridNominalVoltageKv,
    double MeasuredDvDtKilovoltsPerNanosecond,
    double PeakSurgeVoltageKilovolts,
    double NeutralGeomagneticallyInducedCurrentAmperes,
    double TransformerReactivePowerSurgeMvar,
    double TransformerTopOilTempCelsius,
    double FastInterceptionMovConductingCurrentAmperes,
    bool DcBlockingCapacitorEngaged,
    double FaradayShieldIntegrityPercent);

public sealed record SubstationHempAnalysisResult(
    string SubstationId,
    double ClampedResidualVoltageKilovolts,
    double EstimatedCoreTemperatureRiseRateCelsiusPerMinute,
    string HempTransientThreatLevel,
    string FastInterceptionActionRecommendation,
    bool IsSubstationProtected,
    DateTimeOffset Timestamp);
