using FluentAssertions;
using SGrowing.Api.Modules.Finance.FxHedgingEscrow;
using Xunit;

namespace SGrowing.Tests.Finance;

public class FxHedgingEscrowTests
{
    private readonly FxHedgingEscrowEngine _engine = new();

    [Fact]
    public void GetPortfolio_ReturnsActiveForwardContracts()
    {
        var portfolio = _engine.GetPortfolio();

        portfolio.Should().NotBeNull();
        portfolio.ActiveContracts.Should().HaveCount(2);
        portfolio.TotalHedgedNotionalUsd.Should().BeGreaterThan(400000m);
        portfolio.ValueAtRiskPercentage.Should().BeLessThan(5.0);
    }

    [Fact]
    public void LockForward_LocksRateAndComputesGuaranteedAmount()
    {
        var request = new LockForwardRequest(CurrencyCorridor.EurUsd, NotionalAmountSource: 100000m, DurationDays: 30);
        var contract = _engine.LockForward(request);

        contract.Should().NotBeNull();
        contract.Corridor.Should().Be(CurrencyCorridor.EurUsd);
        contract.LockedExchangeRate.Should().Be(1.0875m);
        contract.GuaranteedAmountTarget.Should().Be(108750m);
        contract.IsSettled.Should().BeFalse();
    }

    [Fact]
    public void SettleContract_ComputesVarianceAndMarksSettled()
    {
        var contractId = "fwd_eur_701";
        // Contract locked 250,000 EUR at 1.0850 = $271,250 USD
        // Spot depreciated to 1.0500 -> unhedged would be $262,500 USD (Hedge saved $8,750 USD)
        var request = new SettleEscrowRequest(contractId, SpotRateAtSettlement: 1.0500m);

        var outcome = _engine.SettleContract(request);

        outcome.Success.Should().BeTrue();
        outcome.VarianceCapturedUsd.Should().Be(8750m);
        outcome.Message.Should().Contain("protected from adverse depreciation");

        var portfolio = _engine.GetPortfolio();
        portfolio.ActiveContracts.First(c => c.ContractId == contractId).IsSettled.Should().BeTrue();
    }
}
