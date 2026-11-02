namespace SGrowing.Api.Modules.Energy.SmrNeutronReactor;

public record SmrReactorCoreState(
    string ReactorId,
    double CurrentThermalPowerMw,
    double RatedThermalPowerMw,
    double ThermalNeutronFluxNPerCm2S,
    double FastNeutronFluxNPerCm2S,
    double CoreCoolantInletTempCelsius,
    double CoreCoolantOutletTempCelsius,
    double CurrentBoronConcentrationPpm,
    double ControlRodBankInsertionPercent,
    double Iodine135InventoryAtomsPerCm3,
    double Xenon135InventoryAtomsPerCm3,
    double ShutdownMarginPcm
);

public record XenonTransientEvaluationRequest(
    SmrReactorCoreState CoreState,
    double TargetThermalPowerMw,
    double LookaheadHours = 12.0
);

public record XenonTransientStep(
    double HoursPostTransition,
    double EstimatedXenonReactivityPcm,
    double EstimatedNetReactivityPcm,
    bool CriticalityMaintainable
);

public record ReactivityAdjustmentDecision(
    string ReactorId,
    double RecommendedRodBankPositionPercent,
    double RecommendedBoronConcentrationPpm,
    double PeakNegativeReactivityPcm,
    double TimeToPeakNegativeReactivityHours,
    bool XenonDeadlockRisk,
    string OperatingAction,
    IReadOnlyList<XenonTransientStep> TransientTrajectory
);
