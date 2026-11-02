namespace SGrowing.Tests.Finance;

using FluentAssertions;
using SGrowing.Api.Modules.Finance.MicroPaymentMesh;
using Xunit;

public class MicroPaymentMeshTests
{
    private readonly MicroPaymentChannelMeshEngine _engine = new();

    [Fact]
    public void RouteMicroPayment_MultiHopRoute_FindsCheapestPathAndConstructsHops()
    {
        // Topology: Alice -> Bob -> Carol
        var channels = new List<PaymentChannelEdge>
        {
            new("CH-ALICE-BOB", "ALICE", "BOB", CapacityTokens: 100m, BalanceA: 50m, BalanceB: 50m, FeeBaseTokens: 0.01m, FeeProportionalPpm: 100m, IsOpen: true),
            new("CH-BOB-CAROL", "BOB", "CAROL", CapacityTokens: 100m, BalanceA: 40m, BalanceB: 60m, FeeBaseTokens: 0.02m, FeeProportionalPpm: 200m, IsOpen: true)
        };

        var request = new RoutePaymentRequest("ALICE", "CAROL", PaymentAmountTokens: 10m, "HASH-PAYMENT-001", channels);
        var result = _engine.RouteMicroPayment(request);

        result.IsSuccessful.Should().BeTrue();
        result.Hops.Should().HaveCount(2);
        result.Hops[0].FromNode.Should().Be("ALICE");
        result.Hops[0].ToNode.Should().Be("BOB");
        result.Hops[1].FromNode.Should().Be("BOB");
        result.Hops[1].ToNode.Should().Be("CAROL");
        result.TotalFeeTokens.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void RouteMicroPayment_InsufficientChannelLiquidity_FailsWithDescriptiveStatus()
    {
        // Alice -> Dave has only 2 tokens, but payment is 10 tokens
        var channels = new List<PaymentChannelEdge>
        {
            new("CH-ALICE-DAVE", "ALICE", "DAVE", CapacityTokens: 10m, BalanceA: 2m, BalanceB: 8m, FeeBaseTokens: 0.01m, FeeProportionalPpm: 50m, IsOpen: true)
        };

        var request = new RoutePaymentRequest("ALICE", "DAVE", PaymentAmountTokens: 10m, "HASH-NO-LIQ", channels);
        var result = _engine.RouteMicroPayment(request);

        result.IsSuccessful.Should().BeFalse();
        result.Hops.Should().BeEmpty();
        result.StatusMessage.Should().Contain("sufficient liquidity capacity");
    }

    [Fact]
    public void RouteMicroPayment_SameSourceAndDestination_ReturnsZeroHopsSuccessful()
    {
        var request = new RoutePaymentRequest("NODE-X", "NODE-X", 5m, "HASH-SELF", []);
        var result = _engine.RouteMicroPayment(request);

        result.IsSuccessful.Should().BeTrue();
        result.Hops.Should().BeEmpty();
        result.TotalFeeTokens.Should().Be(0m);
    }

    [Fact]
    public void RouteMicroPayment_ClosedChannel_IgnoresInactiveChannels()
    {
        var channels = new List<PaymentChannelEdge>
        {
            new("CH-CLOSED", "NODE-1", "NODE-2", 100m, 50m, 50m, 0.01m, 100m, IsOpen: false) // Closed channel
        };

        var request = new RoutePaymentRequest("NODE-1", "NODE-2", 5m, "HASH-CLOSED", channels);
        var result = _engine.RouteMicroPayment(request);

        result.IsSuccessful.Should().BeFalse();
    }
}
