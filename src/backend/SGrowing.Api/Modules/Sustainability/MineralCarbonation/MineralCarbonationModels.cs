namespace SGrowing.Api.Modules.Sustainability.MineralCarbonation;

public record MineralFeedstock(
    string FeedstockId,
    string MineralType,
    double MassTons,
    double ReactiveOxideFraction,
    double ParticleSizeMicrons);

public record FlueGasStream(
    string SourceFacility,
    double FlowRateNm3PerHour,
    double Co2ConcentrationPercent,
    double InletTemperatureCelsius,
    double PressureBar);

public record ReactorOperatingParameters(
    double ReactorTemperatureCelsius = 150.0,
    double PartialPressureCo2Bar = 25.0,
    double ResidenceTimeHours = 2.0,
    double CatalyticAdditivesKgPerTon = 5.0);

public record MineralCarbonationBatchRequest(
    string BatchId,
    MineralFeedstock Feedstock,
    FlueGasStream FlueGas,
    ReactorOperatingParameters Parameters);

public record MineralCarbonationBatchResult(
    string BatchId,
    double Co2SequesteredTons,
    double CarbonationEfficiencyPercent,
    double NetExothermicHeatRecoveryMwh,
    double CarbonCreditsGenerated,
    string SolidProductDisposition,
    string RegulatoryCertificationStatus);
