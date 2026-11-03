using FluentAssertions;
using SGrowing.Api.Modules.Finance.AssetDepreciation;
using Xunit;

namespace SGrowing.Tests.Finance;

public class AssetDepreciationTests
{
    private readonly AssetDepreciationEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsFixedAssetsAndBookValue()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalFixedAssetsCount.Should().Be(2);
        overview.TotalCostBasisUsd.Should().Be(3350000.00m);
        overview.NetBookValueUsd.Should().BeGreaterThan(2000000.00m);
        overview.CurrentMonthDepreciationExpenseUsd.Should().BeGreaterThan(50000.00m);
        overview.FixedAssets.Should().HaveCount(2);
        overview.RecentSchedules.Should().HaveCount(1);
    }

    [Fact]
    public void CalculateDepreciation_ComputesAccurateStraightLineExpense()
    {
        var request = new CalculateDepreciationRequest(
            "AST-EDGE-POP-02",
            850000.00m,
            85000.00m,
            5,
            "STRAIGHT_LINE",
            2026
        );

        var result = _engine.CalculateDepreciation(request);

        result.Should().NotBeNull();
        result.AssetTag.Should().Be("AST-EDGE-POP-02");
        result.AnnualDepreciationExpenseUsd.Should().Be(153000.00m); // (850000 - 85000) / 5
        result.MonthlyDepreciationUsd.Should().Be(12750.00m); // 153000 / 12
        result.YearEndBookValueUsd.Should().Be(697000.00m); // 850000 - 153000
    }

    [Fact]
    public void RecordImpairment_RecognizesLossAndIssuesJournalReceipt()
    {
        var request = new RecordAssetImpairmentRequest(
            "AST-GPU-H100-01",
            1200000.00m,
            "Next-generation Blackwell B200 architecture secondary market price drop"
        );

        var receipt = _engine.RecordImpairment(request);

        receipt.Should().NotBeNull();
        receipt.JournalEntryId.Should().StartWith("JRN-IMP-");
        receipt.AssetTag.Should().Be("AST-GPU-H100-01");
        receipt.ImpairmentLossRecognizedUsd.Should().BeGreaterThan(0m);
        receipt.Status.Should().Be("IMPAIRMENT_POSTED_GENERAL_LEDGER");
    }
}
