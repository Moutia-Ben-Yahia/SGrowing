using System;
using SGrowing.Api.Modules.Quantum.NeutralAtomTweezerRearrangement;
using Xunit;

namespace SGrowing.Tests.Quantum;

public sealed class NeutralAtomTweezerRearrangementTests
{
    private readonly NeutralAtomTweezerRearrangementEngine _engine = new();

    [Fact]
    public void AnalyzeRearrangement_OptimalRegister_ReturnsDefectFreeReady()
    {
        var telemetry = new NeutralAtomTweezerTelemetry
        {
            RegisterId = "Q-NAT-100",
            AtomicSpecies = "Rb-87",
            TargetSitesCount = 100,
            InitialLoadedAtomsCount = 160,
            TrapWaistUm = 1.05,
            TrapFrequencyKhz = 120.0,
            MaxShuttleVelocityMmPerSec = 40.0,
            MaxShuttleAccelerationMPerSec2 = 300.0,
            MinInterAtomClearanceUm = 4.5,
            TotalSortingDurationMs = 35.0,
            VacuumLifetimeSec = 30.0,
            UnfilledDefectVacanciesCount = 0
        };

        var result = _engine.AnalyzeRearrangement(telemetry);

        Assert.Equal("Q-NAT-100", result.RegisterId);
        Assert.True(result.IsCollisionlessSeparationMaintained);
        Assert.True(result.IsAdiabaticTransportPreserved);
        Assert.True(result.IsDefectFreeRegisterReady);
        Assert.Equal(0.0, result.VacancyDefectRatePercent);
        Assert.True(result.TotalWavepacketSurvivalProbability >= 0.95);
        Assert.Equal("DETERMINISTIC_DEFECT_FREE_REGISTER_READY", result.RearrangementRegime);
    }

    [Fact]
    public void AnalyzeRearrangement_SubCriticalClearance_TriggersCollisionHazard()
    {
        var telemetry = new NeutralAtomTweezerTelemetry
        {
            RegisterId = "Q-NAT-COLLIDE",
            AtomicSpecies = "Rb-87",
            TargetSitesCount = 64,
            InitialLoadedAtomsCount = 100,
            TrapWaistUm = 1.0,
            TrapFrequencyKhz = 100.0,
            MaxShuttleVelocityMmPerSec = 30.0,
            MaxShuttleAccelerationMPerSec2 = 200.0,
            MinInterAtomClearanceUm = 2.1, // Below 3.0 um
            TotalSortingDurationMs = 40.0,
            VacuumLifetimeSec = 25.0,
            UnfilledDefectVacanciesCount = 0
        };

        var result = _engine.AnalyzeRearrangement(telemetry);

        Assert.False(result.IsCollisionlessSeparationMaintained);
        Assert.Equal("INTERTWEEZER_CROSSTALK_COLLISION_HAZARD", result.RearrangementRegime);
        Assert.Contains("violated critical clearance threshold", result.MitigationRecommendation);
    }

    [Fact]
    public void AnalyzeRearrangement_HighVelocity_TriggersNonAdiabaticExcitation()
    {
        var telemetry = new NeutralAtomTweezerTelemetry
        {
            RegisterId = "Q-NAT-NONADIABATIC",
            AtomicSpecies = "Rb-87",
            TargetSitesCount = 64,
            InitialLoadedAtomsCount = 100,
            TrapWaistUm = 1.0,
            TrapFrequencyKhz = 10.0, // Low trap frequency
            MaxShuttleVelocityMmPerSec = 150.0, // High velocity
            MaxShuttleAccelerationMPerSec2 = 600.0,
            MinInterAtomClearanceUm = 4.0,
            TotalSortingDurationMs = 20.0,
            VacuumLifetimeSec = 25.0,
            UnfilledDefectVacanciesCount = 0
        };

        var result = _engine.AnalyzeRearrangement(telemetry);

        Assert.False(result.IsAdiabaticTransportPreserved);
        Assert.Equal("NON_ADIABATIC_MOTIONAL_EXCITATION_COLLAPSE", result.RearrangementRegime);
        Assert.Contains("Reduce maximum shuttle velocity", result.MitigationRecommendation);
    }

    [Fact]
    public void AnalyzeRearrangement_ResidualVacancies_FlagsDefectAssembly()
    {
        var telemetry = new NeutralAtomTweezerTelemetry
        {
            RegisterId = "Q-NAT-DEFECT",
            AtomicSpecies = "Yb-171",
            TargetSitesCount = 100,
            InitialLoadedAtomsCount = 110,
            TrapWaistUm = 1.1,
            TrapFrequencyKhz = 110.0,
            MaxShuttleVelocityMmPerSec = 35.0,
            MaxShuttleAccelerationMPerSec2 = 250.0,
            MinInterAtomClearanceUm = 3.8,
            TotalSortingDurationMs = 45.0,
            VacuumLifetimeSec = 25.0,
            UnfilledDefectVacanciesCount = 4 // 4 vacancies remaining
        };

        var result = _engine.AnalyzeRearrangement(telemetry);

        Assert.False(result.IsDefectFreeRegisterReady);
        Assert.Equal(4.0, result.VacancyDefectRatePercent);
        Assert.Equal("DEFECT_VACANCY_RESIDUAL_SUB_UNITY_ASSEMBLY", result.RearrangementRegime);
        Assert.Contains("Execute secondary replenishment pass", result.MitigationRecommendation);
    }

    [Fact]
    public void AnalyzeRearrangement_NullOrZeroTarget_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeRearrangement(null!));

        var invalidTelemetry = new NeutralAtomTweezerTelemetry
        {
            RegisterId = "Q-NAT-INV",
            AtomicSpecies = "Rb-87",
            TargetSitesCount = 0,
            InitialLoadedAtomsCount = 10
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeRearrangement(invalidTelemetry));
    }
}
