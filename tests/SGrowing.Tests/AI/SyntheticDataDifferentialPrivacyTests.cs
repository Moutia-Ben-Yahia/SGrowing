using FluentAssertions;
using SGrowing.Api.Modules.AI.SyntheticData;
using Xunit;

namespace SGrowing.Tests.AI;

public sealed class SyntheticDataDifferentialPrivacyTests
{
    private readonly DifferentialPrivacyEngine _dpEngine = new();
    private readonly CopulaSyntheticDataGenerator _generator;

    public SyntheticDataDifferentialPrivacyTests()
    {
        _generator = new CopulaSyntheticDataGenerator(_dpEngine);
    }

    #region Differential Privacy Engine Tests

    [Fact]
    public void PerturbQuery_LaplaceMechanism_InjectsNoiseAndDecrementsBudget()
    {
        var tenantId = "TENANT-DP-TEST-01";
        _dpEngine.ResetBudget(tenantId, newTotalEpsilon: 5.0);

        var req = new PerturbQueryRequest(
            TenantId: tenantId,
            QueryIdentifier: "Q-COUNT-PREMIUM-CUSTOMERS",
            RawValue: 1250.0,
            SensitivityDeltaF: 1.0, // Count query sensitivity is 1
            RequestedEpsilon: 0.5,
            Mechanism: NoiseMechanism.Laplace
        );

        var result = _dpEngine.PerturbQuery(req);

        result.Should().NotBeNull();
        result.QueryIdentifier.Should().Be("Q-COUNT-PREMIUM-CUSTOMERS");
        result.EpsilonCost.Should().Be(0.5);
        result.RemainingEpsilon.Should().Be(4.5);
        result.MechanismUsed.Should().Be(NoiseMechanism.Laplace);
        // Perturbed value should be near raw value
        result.PerturbedValue.Should().BeInRange(1200.0, 1300.0);
    }

    [Fact]
    public void PerturbQuery_GaussianMechanism_ExecutesWithDeltaBound()
    {
        var tenantId = "TENANT-DP-GAUSSIAN";
        _dpEngine.ResetBudget(tenantId, newTotalEpsilon: 2.0);

        var req = new PerturbQueryRequest(
            TenantId: tenantId,
            QueryIdentifier: "Q-AVG-ORDER-VALUE",
            RawValue: 85.0,
            SensitivityDeltaF: 5.0,
            RequestedEpsilon: 0.25,
            Mechanism: NoiseMechanism.Gaussian,
            RequestedDelta: 1e-5
        );

        var result = _dpEngine.PerturbQuery(req);

        result.MechanismUsed.Should().Be(NoiseMechanism.Gaussian);
        result.RemainingEpsilon.Should().Be(1.75);
    }

    [Fact]
    public void PerturbQuery_ExhaustedBudget_ThrowsInvalidOperationException()
    {
        var tenantId = "TENANT-EXHAUSTION-TEST";
        _dpEngine.ResetBudget(tenantId, newTotalEpsilon: 0.5);

        // First query consumes 0.4 epsilon -> 0.1 remaining
        _dpEngine.PerturbQuery(new PerturbQueryRequest(tenantId, "Q1", 100.0, 1.0, 0.4));

        // Second query attempts to consume 0.2 epsilon > 0.1 remaining -> Must Throw
        var act = () => _dpEngine.PerturbQuery(new PerturbQueryRequest(tenantId, "Q2", 100.0, 1.0, 0.2));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Privacy budget exhausted*");
    }

    [Fact]
    public void PerturbQuery_InvalidParameters_ThrowsExpectedExceptions()
    {
        var tenantId = "TENANT-PARAMS-TEST";

        // 1. Null request
        var nullAct = () => _dpEngine.PerturbQuery(null!);
        nullAct.Should().Throw<ArgumentNullException>();

        // 2. Zero epsilon
        var zeroEpsAct = () => _dpEngine.PerturbQuery(new PerturbQueryRequest(tenantId, "Q1", 10.0, 1.0, 0.0));
        zeroEpsAct.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("RequestedEpsilon");

        // 3. Zero sensitivity
        var zeroSensAct = () => _dpEngine.PerturbQuery(new PerturbQueryRequest(tenantId, "Q1", 10.0, 0.0, 0.5));
        zeroSensAct.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("SensitivityDeltaF");
    }

    [Fact]
    public void GetBudget_UninitializedTenant_ReturnsDefaultBudget()
    {
        var tenantId = "TENANT-FRESH-001";
        var budget = _dpEngine.GetBudget(tenantId);

        budget.Should().NotBeNull();
        budget.TenantId.Should().Be(tenantId);
        budget.TotalEpsilon.Should().Be(5.0);
        budget.ConsumedEpsilon.Should().Be(0.0);
        budget.Delta.Should().Be(1e-5);
    }

    #endregion

    #region Synthetic Data Generation Tests

    [Fact]
    public void GenerateLeads_ValidRequest_SynthesizesNonEmptyPopulatedProfiles()
    {
        var tenantId = "TENANT-SYNTH-LEADS";
        _dpEngine.ResetBudget(tenantId, newTotalEpsilon: 10.0);

        var request = new GenerateSyntheticDatasetRequest(
            TenantId: tenantId,
            RecordCount: 5,
            EpsilonBudget: 0.5,
            IndustryFocus: "Pharmaceutical Logistics"
        );

        var leads = _generator.GenerateLeads(request);

        leads.Should().HaveCount(5);
        foreach (var lead in leads)
        {
            lead.SyntheticId.Should().StartWith("SYNTH-");
            lead.FullName.Should().NotBeNullOrWhiteSpace();
            lead.SyntheticCompany.Should().Contain("SPA");
            lead.Industry.Should().Be("Pharmaceutical Logistics");
            lead.EstimatedBudgetEur.Should().BeGreaterThan(0.0m);
            lead.EngagementScore.Should().BeInRange(0.0, 1.0);
        }
    }

    [Fact]
    public void GenerateLeads_ZeroOrNegativeRecordCount_ReturnsEmptyCollection()
    {
        var requestZero = new GenerateSyntheticDatasetRequest("TENANT-ZERO", RecordCount: 0);
        var requestNeg = new GenerateSyntheticDatasetRequest("TENANT-NEG", RecordCount: -3);

        _generator.GenerateLeads(requestZero).Should().BeEmpty();
        _generator.GenerateLeads(requestNeg).Should().BeEmpty();
    }

    [Fact]
    public void GenerateLeads_NullRequest_ThrowsArgumentNullException()
    {
        var act = () => _generator.GenerateLeads(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateLeads_WhenDpBudgetExhausted_GracefullyFallsBackToSample()
    {
        var tenantId = "TENANT-EXHAUST-FALLBACK";
        _dpEngine.ResetBudget(tenantId, newTotalEpsilon: 0.001);

        // Exhaust budget
        _dpEngine.PerturbQuery(new PerturbQueryRequest(tenantId, "Q-DEPLETE", 100.0, 1.0, 0.001));

        var request = new GenerateSyntheticDatasetRequest(
            TenantId: tenantId,
            RecordCount: 3,
            EpsilonBudget: 2.0,
            IndustryFocus: "Renewable Energy"
        );

        // Should NOT throw, but fall back gracefully
        var leads = _generator.GenerateLeads(request);

        leads.Should().HaveCount(3);
        leads.Should().AllSatisfy(l =>
        {
            l.Industry.Should().Be("Renewable Energy");
            l.EstimatedBudgetEur.Should().BeGreaterThan(0m);
        });
    }

    #endregion
}
