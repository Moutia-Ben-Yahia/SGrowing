using FluentAssertions;
using SGrowing.Api.Modules.BioTech.MicrofluidicLnpMrnaEncapsulation;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class MicrofluidicLnpMrnaEncapsulationTests
{
    private readonly MicrofluidicLnpMrnaEncapsulationEngine _engine = new();

    [Fact]
    public void AnalyzeLnpFormulation_NominalMixing_ReturnsMonodisperseOptimal()
    {
        var telemetry = new LnpFormulationTelemetry(
            FormulationBatchId: "BATCH-LNP-MRNA-BNT162B2",
            AqueousMrnaFlowRateMlPerMin: 15.0,
            EthanolLipidFlowRateMlPerMin: 5.0, // FRR = 3.0, TFR = 20.0
            IonizableNitrogenToMrnaPhosphateRatio: 6.0,
            MeasuredHydrodynamicDiameterNanometers: 72.0,
            MeasuredPolydispersityIndex: 0.08,
            MicrochannelDifferentialPressureBar: 1.8,
            MeasuredEncapsulationEfficiencyPercent: 94.5,
            InlineTangentialFlowFiltrationActive: true
        );

        var result = _engine.AnalyzeLnpFormulation(telemetry);

        result.FormulationRegime.Should().Be("MONODISPERSE_NANO_ENCAPSULATION_OPTIMAL");
        result.IsBatchClinicallyAcceptable.Should().BeTrue();
        result.FlowRateRatioAqueousToEthanol.Should().Be(3.0);
        result.TotalFlowRateMlPerMin.Should().Be(20.0);
        result.NanoparticleQualityScore.Should().BeGreaterThan(0.75);
    }

    [Fact]
    public void AnalyzeLnpFormulation_HighPressure_ReturnsMicrochannelClogging()
    {
        var telemetry = new LnpFormulationTelemetry(
            FormulationBatchId: "BATCH-LNP-CLOGGED-02",
            AqueousMrnaFlowRateMlPerMin: 15.0,
            EthanolLipidFlowRateMlPerMin: 5.0,
            IonizableNitrogenToMrnaPhosphateRatio: 6.0,
            MeasuredHydrodynamicDiameterNanometers: 78.0,
            MeasuredPolydispersityIndex: 0.09,
            MicrochannelDifferentialPressureBar: 5.5, // High backpressure > 4.2 bar
            MeasuredEncapsulationEfficiencyPercent: 92.0,
            InlineTangentialFlowFiltrationActive: true
        );

        var result = _engine.AnalyzeLnpFormulation(telemetry);

        result.FormulationRegime.Should().Be("MICROCHANNEL_CLOGGING_PRESSURE_SPIKE");
        result.IsBatchClinicallyAcceptable.Should().BeFalse();
        result.FormulationPumpAction.Should().Contain("ethanol back-flush");
    }

    [Fact]
    public void AnalyzeLnpFormulation_HighPdiAggregation_ReturnsAggregationReject()
    {
        var telemetry = new LnpFormulationTelemetry(
            FormulationBatchId: "BATCH-LNP-AGGREGATE-03",
            AqueousMrnaFlowRateMlPerMin: 6.0,
            EthanolLipidFlowRateMlPerMin: 2.0, // Low flow rate -> poor mixing
            IonizableNitrogenToMrnaPhosphateRatio: 6.0,
            MeasuredHydrodynamicDiameterNanometers: 145.0, // Large aggregate
            MeasuredPolydispersityIndex: 0.24, // High PDI > 0.15
            MicrochannelDifferentialPressureBar: 2.0,
            MeasuredEncapsulationEfficiencyPercent: 89.0,
            InlineTangentialFlowFiltrationActive: false
        );

        var result = _engine.AnalyzeLnpFormulation(telemetry);

        result.FormulationRegime.Should().Be("HIGH_PDI_AGGREGATION_REJECT");
        result.IsBatchClinicallyAcceptable.Should().BeFalse();
        result.FormulationPumpAction.Should().Contain("accelerate convective chaotic mixing");
    }

    [Fact]
    public void AnalyzeLnpFormulation_LowNpRatio_ReturnsSuboptimalEncapsulation()
    {
        var telemetry = new LnpFormulationTelemetry(
            FormulationBatchId: "BATCH-LNP-LOW-NP-04",
            AqueousMrnaFlowRateMlPerMin: 15.0,
            EthanolLipidFlowRateMlPerMin: 5.0,
            IonizableNitrogenToMrnaPhosphateRatio: 3.2, // Low N/P < 4.5
            MeasuredHydrodynamicDiameterNanometers: 68.0,
            MeasuredPolydispersityIndex: 0.10,
            MicrochannelDifferentialPressureBar: 1.6,
            MeasuredEncapsulationEfficiencyPercent: 74.0, // Low EE < 88%
            InlineTangentialFlowFiltrationActive: true
        );

        var result = _engine.AnalyzeLnpFormulation(telemetry);

        result.FormulationRegime.Should().Be("SUBOPTIMAL_NP_RATIO_LOW_ENCAPSULATION");
        result.IsBatchClinicallyAcceptable.Should().BeFalse();
        result.FormulationPumpAction.Should().Contain("Titrate ionizable lipid stock");
    }
}
