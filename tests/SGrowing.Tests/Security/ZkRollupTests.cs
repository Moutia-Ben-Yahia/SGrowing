using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.ZkRollupBridge;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class ZkRollupTests
{
    private readonly ZkRollupBatchAggregatorEngine _engine = new();

    [Fact]
    public void ProcessBatch_ValidTransactions_UpdatesStateAndPreservesBalanceConservation()
    {
        // Arrange
        var initialAccounts = new Dictionary<string, RollupAccountState>
        {
            ["ACC-ALICE"] = new("ACC-ALICE", 1000.0m, 0),
            ["ACC-BOB"] = new("ACC-BOB", 500.0m, 0),
            ["ACC-CHARLIE"] = new("ACC-CHARLIE", 200.0m, 0)
        };

        var initialTotalBalance = initialAccounts.Values.Sum(a => a.BalanceEur);

        var tx1 = new RollupTransaction("TX-01", "ACC-ALICE", "ACC-BOB", 150.0m, 1.5m, 1, "SIG-1");
        var tx2 = new RollupTransaction("TX-02", "ACC-BOB", "ACC-CHARLIE", 50.0m, 0.5m, 1, "SIG-2");

        var request = new ProcessBatchRequest(
            Transactions: [tx1, tx2],
            InitialAccounts: initialAccounts
        );

        // Act
        var response = _engine.ProcessBatch(request);

        // Assert
        response.Commitment.TxCount.Should().Be(2);
        response.Commitment.TotalTransferredEur.Should().Be(200.0m);
        response.Commitment.TotalFeesEur.Should().Be(2.0m);
        response.Commitment.RootPreHex.Should().NotBe(response.Commitment.RootPostHex);
        response.Commitment.PolynomialCommitmentRootHex.Should().NotBeNullOrEmpty();

        // Check account balances
        var alice = response.UpdatedAccounts["ACC-ALICE"];
        alice.BalanceEur.Should().Be(1000.0m - 151.5m); // 848.5 EUR
        alice.Nonce.Should().Be(1);

        var bob = response.UpdatedAccounts["ACC-BOB"];
        bob.BalanceEur.Should().Be(500.0m + 150.0m - 50.5m); // 599.5 EUR
        bob.Nonce.Should().Be(1);

        var charlie = response.UpdatedAccounts["ACC-CHARLIE"];
        charlie.BalanceEur.Should().Be(200.0m + 50.0m); // 250.0 EUR

        // Invariant: Initial Balance = Updated Balance + Fees
        var updatedTotalBalance = response.UpdatedAccounts.Values.Sum(a => a.BalanceEur);
        (updatedTotalBalance + response.Commitment.TotalFeesEur).Should().Be(initialTotalBalance);
    }

    [Fact]
    public void VerifyInclusionProof_ValidProof_ReturnsTrue()
    {
        // Arrange
        var initialAccounts = new Dictionary<string, RollupAccountState>
        {
            ["ACC-1"] = new("ACC-1", 500.0m, 0),
            ["ACC-2"] = new("ACC-2", 100.0m, 0),
            ["ACC-3"] = new("ACC-3", 100.0m, 0)
        };

        var tx1 = new RollupTransaction("TX-01", "ACC-1", "ACC-2", 20.0m, 0.1m, 1, "SIG-1");
        var tx2 = new RollupTransaction("TX-02", "ACC-1", "ACC-3", 30.0m, 0.1m, 2, "SIG-2");
        var tx3 = new RollupTransaction("TX-03", "ACC-2", "ACC-3", 10.0m, 0.1m, 1, "SIG-3");

        var response = _engine.ProcessBatch(new ProcessBatchRequest([tx1, tx2, tx3], initialAccounts));

        // Act & Assert each transaction's Merkle inclusion proof
        response.InclusionProofs.Should().HaveCount(3);
        foreach (var proof in response.InclusionProofs)
        {
            var isValid = _engine.VerifyInclusionProof(proof);
            isValid.Should().BeTrue();
        }
    }

    [Fact]
    public void ProcessBatch_InsufficientSenderBalance_ThrowsInvalidOperationException()
    {
        // Arrange
        var accounts = new Dictionary<string, RollupAccountState>
        {
            ["ACC-BROKE"] = new("ACC-BROKE", 10.0m, 0),
            ["ACC-RICH"] = new("ACC-RICH", 1000.0m, 0)
        };

        var tx = new RollupTransaction("TX-FAIL", "ACC-BROKE", "ACC-RICH", 100.0m, 1.0m, 1, "SIG");

        // Act & Assert
        var act = () => _engine.ProcessBatch(new ProcessBatchRequest([tx], accounts));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*has insufficient balance*");
    }

    [Fact]
    public void ProcessBatch_InvalidNonce_ThrowsInvalidOperationException()
    {
        // Arrange
        var accounts = new Dictionary<string, RollupAccountState>
        {
            ["ACC-SENDER"] = new("ACC-SENDER", 500.0m, 5) // Current nonce is 5
        };

        // Tx supplies nonce 1 instead of expected 6
        var tx = new RollupTransaction("TX-REPLAY", "ACC-SENDER", "ACC-RECEIVER", 20.0m, 0.1m, 1, "SIG");

        // Act & Assert
        var act = () => _engine.ProcessBatch(new ProcessBatchRequest([tx], accounts));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid nonce*expected 6*");
    }

    [Fact]
    public void AddZkRollupBridgeServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddZkRollupBridgeServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(ZkRollupBatchAggregatorEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
