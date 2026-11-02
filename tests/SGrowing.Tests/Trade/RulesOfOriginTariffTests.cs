namespace SGrowing.Tests.Trade;

using FluentAssertions;
using SGrowing.Api.Modules.Trade.RulesOfOriginTariff;
using Xunit;

public class RulesOfOriginTariffTests
{
    private readonly RulesOfOriginTariffEngine _engine = new();

    [Fact]
    public void EvaluateOriginAndTariffs_HighRvc_QualifiesForPreferentialZeroTariff()
    {
        var components = new List<BomMaterialComponent>
        {
            new("PART-CHASSIS", "Aluminum Stamped Frame", "7616.99", "USA", 250.0m, IsOriginating: true),
            new("PART-MOTOR", "Electric Traction Drive", "8501.32", "MEXICO", 400.0m, IsOriginating: true),
            new("PART-IC", "Power Microcontroller", "8542.31", "TAIWAN", 80.0m, IsOriginating: false)
        };

        // Transaction value 1000 USD, non-originating is 80 USD => RVC = 92% > 60% USMCA threshold
        var request = new RulesOfOriginEvaluationRequest(
            FinishedProductPartNumber: "EV-POWER-UNIT-500",
            FinishedProductHsCode: "8708.99",
            ExportingCountry: "MEXICO",
            DestinationCountry: "USA",
            TradeAgreementCode: "USMCA",
            TransactionValueUsd: 1000.0m,
            NetCostUsd: 800.0m,
            Components: components
        );

        var result = _engine.EvaluateOriginAndTariffs(request);

        result.QualifiesForPreferentialTariff.Should().BeTrue();
        result.AppliedTariffRatePercent.Should().Be(0.0m);
        result.AntiDumpingCircumventionRisk.Should().BeFalse();
        result.RegionalValueContentPercent.Should().Be(92.0m);
        result.EstimatedTariffSavingsUsd.Should().Be(85.0m); // 8.5% of 1000 USD
        result.ComplianceJustification.Should().Contain("PREFERENTIAL_ORIGIN_GRANTED");
    }

    [Fact]
    public void EvaluateOriginAndTariffs_CircumventionRisk_AppliesAntiDumpingPenaltyTariff()
    {
        // 80% non-originating components sharing identical finished heading (8504.40) with no substantial transformation
        var components = new List<BomMaterialComponent>
        {
            new("BOARD-PCB", "Assembled Power Inverter Core", "8504.40", "CHINA", 800.0m, IsOriginating: false),
            new("CASE-PLASTIC", "Enclosure Shell", "3926.90", "VIETNAM", 50.0m, IsOriginating: true)
        };

        var request = new RulesOfOriginEvaluationRequest(
            FinishedProductPartNumber: "INVERTER-PACK",
            FinishedProductHsCode: "8504.40",
            ExportingCountry: "VIETNAM",
            DestinationCountry: "USA",
            TradeAgreementCode: "USMCA",
            TransactionValueUsd: 950.0m,
            NetCostUsd: 900.0m,
            Components: components
        );

        var result = _engine.EvaluateOriginAndTariffs(request);

        result.AntiDumpingCircumventionRisk.Should().BeTrue();
        result.QualifiesForPreferentialTariff.Should().BeFalse();
        result.AppliedTariffRatePercent.Should().Be(33.5m); // 8.5% MFN + 25% AD penalty
        result.ComplianceJustification.Should().Contain("CIRCUMVENTION_AUDIT_FAIL");
    }

    [Fact]
    public void EvaluateOriginAndTariffs_InsufficientRvc_FallsBackToStandardMfnRate()
    {
        var components = new List<BomMaterialComponent>
        {
            new("PART-A", "Component A", "8482.10", "GERMANY", 600.0m, IsOriginating: false),
            new("PART-B", "Component B", "8483.20", "USA", 100.0m, IsOriginating: true)
        };

        // Transaction value 800 USD, non-originating is 600 USD => RVC = 25% (< 60% USMCA, < 40% CTH minimum)
        var request = new RulesOfOriginEvaluationRequest(
            FinishedProductPartNumber: "GEARBOX-X",
            FinishedProductHsCode: "8483.40",
            ExportingCountry: "USA",
            DestinationCountry: "CANADA",
            TradeAgreementCode: "USMCA",
            TransactionValueUsd: 800.0m,
            NetCostUsd: 750.0m,
            Components: components
        );

        var result = _engine.EvaluateOriginAndTariffs(request);

        result.QualifiesForPreferentialTariff.Should().BeFalse();
        result.AppliedTariffRatePercent.Should().Be(8.5m);
        result.ComplianceJustification.Should().Contain("STANDARD_MFN_APPLIED");
    }

    [Fact]
    public void EvaluateOriginAndTariffs_TariffShiftSatisfied_QualifiesUnderCthSubstantialTransformation()
    {
        var components = new List<BomMaterialComponent>
        {
            new("STEEL-ROD", "Hot-Rolled Steel Rods", "7214.20", "BRAZIL", 450.0m, IsOriginating: false),
            new("ZINC-COAT", "Zinc Galvanizing Bath", "7901.11", "JAPAN", 50.0m, IsOriginating: false)
        };

        // Finished item is classified under completely different heading 7318 (screws/bolts)
        // RVC is (1000 - 500)/1000 = 50% >= 40% with CTH shift
        var request = new RulesOfOriginEvaluationRequest(
            FinishedProductPartNumber: "HEAVY-STRUCTURAL-FASTENERS",
            FinishedProductHsCode: "7318.15",
            ExportingCountry: "JAPAN",
            DestinationCountry: "AUSTRALIA",
            TradeAgreementCode: "CPTPP",
            TransactionValueUsd: 1000.0m,
            NetCostUsd: 800.0m,
            Components: components
        );

        var result = _engine.EvaluateOriginAndTariffs(request);

        result.QualifiesForPreferentialTariff.Should().BeTrue();
        result.AppliedTariffRatePercent.Should().Be(0.0m);
    }
}
