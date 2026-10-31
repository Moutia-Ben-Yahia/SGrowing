using FluentAssertions;
using SGrowing.Api.Modules.Security.SyntheticData;
using Xunit;

namespace SGrowing.Tests.Security;

public class SyntheticDataGeneratorServiceTests
{
    private readonly SyntheticDataGeneratorService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task InspectAndRecommendRulesAsync_ShouldClassifyDirectIdentifiers()
    {
        var cols = new List<string> { "CustomerFullName", "WorkEmail", "MobilePhone", "TaxIdNumber", "BaseSalary", "PostalCode" };
        var rules = await _service.InspectAndRecommendRulesAsync(cols);

        rules.Should().HaveCount(6);
        rules.First(r => r.ColumnName == "CustomerFullName").Classification.Should().Be(DataClassification.DirectIdentifier);
        rules.First(r => r.ColumnName == "WorkEmail").Classification.Should().Be(DataClassification.DirectIdentifier);
        rules.First(r => r.ColumnName == "TaxIdNumber").Strategy.Should().Be(SynthesisStrategy.FormatPreservingToken);
        rules.First(r => r.ColumnName == "BaseSalary").Classification.Should().Be(DataClassification.SensitiveAttribute);
        rules.First(r => r.ColumnName == "PostalCode").Classification.Should().Be(DataClassification.QuasiIdentifier);
    }

    [Fact]
    public async Task GenerateSyntheticDatasetAsync_ShouldProduceSpecifiedRowCountAndMaskPii()
    {
        var rules = new List<ColumnSchemaRule>
        {
            new("Id", DataClassification.NonSensitive, SynthesisStrategy.DeterministicHash),
            new("FullName", DataClassification.DirectIdentifier, SynthesisStrategy.RealisticFake),
            new("Email", DataClassification.DirectIdentifier, SynthesisStrategy.RealisticFake),
            new("Salary", DataClassification.SensitiveAttribute, SynthesisStrategy.DifferentialPrivacyNoise, "decimal", 0.5)
        };

        var req = new GenerateSyntheticDatasetRequest(_tenantId, "Customers", 25, rules);
        var result = await _service.GenerateSyntheticDatasetAsync(req);

        result.Should().NotBeNull();
        result.RowCount.Should().Be(25);
        result.DirectIdentifiersMasked.Should().Be(2);
        result.SampleRecords.Should().NotBeEmpty();
        result.SqlInsertScript.Should().Contain("INSERT INTO [Customers]");

        // Validate sample record keys
        var first = result.SampleRecords[0];
        first["FullName"].Should().NotBeNull();
        first["Email"].ToString().Should().Contain("@");
        first["Id"].ToString().Should().HaveLength(16);
    }

    [Fact]
    public async Task GenerateSyntheticDatasetAsync_ShouldThrow_WhenRowCountExceedsMaxLimit()
    {
        var rules = new List<ColumnSchemaRule>
        {
            new("Name", DataClassification.DirectIdentifier, SynthesisStrategy.RealisticFake)
        };

        var req = new GenerateSyntheticDatasetRequest(_tenantId, "BigTable", 99999, rules);
        var act = () => _service.GenerateSyntheticDatasetAsync(req);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
