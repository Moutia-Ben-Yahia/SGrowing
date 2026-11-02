namespace SGrowing.Tests.Manufacturing;

using SGrowing.Api.Modules.Manufacturing.HighNaEuvStochasticDefects;
using Xunit;

public class HighNaEuvStochasticDefectsTests
{
    private readonly HighNaEuvStochasticDefectsEngine _engine = new();

    [Fact]
    public void AnalyzeWaferField_NominalHighNaField_MaintainsSub2nmYield()
    {
        // NA = 0.55 -> CDmin ~ 8.59 nm, DoF ~ 22.3 nm
        // Dose = 45 mJ/cm2, LER = 1.5 nm, Flare = 4.5% -> Defect density low
        var telemetry = new HighNaEuvTelemetry(
            ScannerId: "ASML-EXE-5000-01",
            WaferFieldId: "WAFER-SUB2NM-SLIT-04",
            NumericalAperture: 0.55,
            TargetCriticalDimensionNm: 10.0,
            MeasuredDoseMilliJoulesPerCm2: 45.0,
            MeasuredLineEdgeRoughness3SigmaNm: 1.5,
            EuvFlareFraction: 0.045,
            AerialImageContrastNils: 2.6,
            ObservedStochasticDefectCount: 1);

        var result = _engine.AnalyzeWaferField(telemetry);

        Assert.Equal("ASML-EXE-5000-01", result.ScannerId);
        Assert.Equal(8.59, result.TheoreticalRayleighResolutionNm);
        Assert.Equal(22.3, result.AnamorphicDepthOfFocusNm);
        Assert.False(result.IsStochasticYieldLimitBreached);
        Assert.Equal("NOMINAL_SUB_2NM_YIELD", result.LithographyProcessStatus);
        Assert.Equal("HOLD_CURRENT_ILLUMINATION_PUPIL", result.DoseAndIlluminationCorrection);
        Assert.True(result.PredictedDefectDensityPerCm2 < 4.0);
    }

    [Fact]
    public void AnalyzeWaferField_LowDoseMicroBridgeRisk_TriggersSourcePowerBoost()
    {
        // Under-exposure: Dose = 32.0 mJ/cm2 (< 38.0)
        var telemetry = new HighNaEuvTelemetry(
            ScannerId: "ASML-EXE-5200-02",
            WaferFieldId: "WAFER-UNDEREXPOSED-02",
            NumericalAperture: 0.55,
            TargetCriticalDimensionNm: 9.0,
            MeasuredDoseMilliJoulesPerCm2: 32.0,
            MeasuredLineEdgeRoughness3SigmaNm: 2.6,
            EuvFlareFraction: 0.050,
            AerialImageContrastNils: 1.9,
            ObservedStochasticDefectCount: 8);

        var result = _engine.AnalyzeWaferField(telemetry);

        Assert.True(result.IsStochasticYieldLimitBreached);
        Assert.Equal("STOCHASTIC_MICRO_BRIDGE_WARNING", result.LithographyProcessStatus);
        Assert.Equal("BOOST_EUV_SOURCE_POWER_12_PERCENT", result.DoseAndIlluminationCorrection);
        Assert.Contains("STOCHASTIC DEFECT RISK", result.MetrologyAdvisory);
    }

    [Fact]
    public void AnalyzeWaferField_ExcessiveOpticalFlare_TriggersAnamorphicSlitMaskAttenuation()
    {
        // High flare fraction: 8.5% (>= 7.5%)
        var telemetry = new HighNaEuvTelemetry(
            ScannerId: "ASML-EXE-5000-FLARE",
            WaferFieldId: "WAFER-FLARE-FIELD-09",
            NumericalAperture: 0.55,
            TargetCriticalDimensionNm: 10.0,
            MeasuredDoseMilliJoulesPerCm2: 48.0,
            MeasuredLineEdgeRoughness3SigmaNm: 1.8,
            EuvFlareFraction: 0.085,
            AerialImageContrastNils: 1.4,
            ObservedStochasticDefectCount: 4);

        var result = _engine.AnalyzeWaferField(telemetry);

        Assert.True(result.IsStochasticYieldLimitBreached);
        Assert.Equal("CRITICAL_NANO_PINHOLE_FLARE_TRIP", result.LithographyProcessStatus);
        Assert.Equal("APPLY_ANAMORPHIC_SLIT_FLARE_MASK_ATTENUATION", result.DoseAndIlluminationCorrection);
        Assert.Contains("EUV FLARE TRIP", result.MetrologyAdvisory);
    }

    [Fact]
    public void AnalyzeWaferField_StandardNaInsteadOfHighNa_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new HighNaEuvTelemetry(
            ScannerId: "ERR-SCANNER",
            WaferFieldId: "W-01",
            NumericalAperture: 0.33, // Standard NA <= 0.33
            TargetCriticalDimensionNm: 13.0,
            MeasuredDoseMilliJoulesPerCm2: 40.0,
            MeasuredLineEdgeRoughness3SigmaNm: 1.5,
            EuvFlareFraction: 0.05,
            AerialImageContrastNils: 2.0,
            ObservedStochasticDefectCount: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.AnalyzeWaferField(invalid));
    }
}
