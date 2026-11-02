namespace SGrowing.Api.Modules.Telecom.SubseaDwdmRamanCompensator;

public record DwdmChannelTelemetry(
    int ChannelIndex,
    double CenterWavelengthNm,
    double LaunchPowerDbm,
    double ReceivedOsnrDb,
    double BitErrorRateQ
);

public record FiberSpanParameters(
    string SpanId = "SPAN-DEFAULT-80KM",
    double SpanLengthKm = 80.0,
    double AttenuationDbPerKm = 0.165,
    double ChromaticDispersionPsPerNmKm = 21.0,
    double EffectiveAreaUm2 = 110.0,
    double RamanGainEfficiencyPerWKm = 0.35
);

public record RamanCompensationRequest(
    FiberSpanParameters Span,
    IReadOnlyList<DwdmChannelTelemetry> Channels,
    double TotalInputPowerDbm = 18.0
);

public record RamanPumpAdjustment(
    int PumpIndex,
    double WavelengthNm,
    double RecommendedPowerMilliwatts,
    string AdjustmentReason
);

public record KerrNonlinearCompensationPlan(
    string SpanId,
    double SpectralPowerTiltDb,
    double NonlinearPhaseShiftRadians,
    double MeanOsnrDb,
    bool NonlinearityThresholdExceeded,
    string OpticalEqualizationStrategy,
    IReadOnlyList<RamanPumpAdjustment> PumpAdjustments
);
