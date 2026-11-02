using FluentAssertions;
using SGrowing.Api.Modules.CRM.MetadataEngine;
using Xunit;

namespace SGrowing.Tests.CRM;

public class CrmMetadataTests
{
    private readonly CrmMetadataEngine _engine = new();

    [Fact]
    public void CreateSchema_RegistersCustomObjectDefinition()
    {
        // Arrange
        var request = new CreateSchemaRequest(
            TenantId: "tenant_crm_01",
            EntityName: "Commercial Property",
            PluralName: "Commercial Properties",
            Icon: "building",
            Description: "Enterprise real estate asset catalog",
            Fields: new List<CustomFieldDefinition>
            {
                new("f1", "propertyName", "Property Name", FieldDataType.Text, true),
                new("f2", "squareFootage", "Square Footage", FieldDataType.Number, true),
                new("f3", "monthlyLeaseRate", "Monthly Lease Rate", FieldDataType.Currency, false),
                new("f4", "isZonedCommercial", "Zoned Commercial", FieldDataType.Boolean, false)
            }
        );

        // Act
        var schema = _engine.CreateSchema(request);

        // Assert
        schema.Should().NotBeNull();
        schema.SchemaId.Should().Be("schema_commercial_property");
        schema.Fields.Should().HaveCount(4);
        schema.Fields.First(f => f.Name == "propertyName").IsRequired.Should().BeTrue();
    }

    [Fact]
    public void CreateRecord_EnforcesRequiredFieldsAndStoresAttributes()
    {
        // Arrange
        var schema = _engine.CreateSchema(new CreateSchemaRequest(
            TenantId: "tenant_crm_02",
            EntityName: "Fleet Vehicle",
            PluralName: "Fleet Vehicles",
            Icon: "truck",
            Description: "Logistics vehicle registry",
            Fields: new List<CustomFieldDefinition>
            {
                new("f1", "vin", "VIN Number", FieldDataType.Text, true),
                new("f2", "odometer", "Odometer", FieldDataType.Number, true)
            }
        ));

        // Act & Assert 1: Missing required field throws ArgumentException
        var invalidReq = new CreateRecordRequest("tenant_crm_02", schema.SchemaId, new Dictionary<string, object>
        {
            ["vin"] = "1HGCR2F83HA001234"
        });
        var act = () => _engine.CreateRecord(invalidReq);
        act.Should().Throw<ArgumentException>().WithMessage("*odometer*");

        // Act & Assert 2: Valid record stores correctly
        var validReq = new CreateRecordRequest("tenant_crm_02", schema.SchemaId, new Dictionary<string, object>
        {
            ["vin"] = "1HGCR2F83HA001234",
            ["odometer"] = 45200
        });
        var record = _engine.CreateRecord(validReq);
        record.Should().NotBeNull();
        record.Attributes["vin"].Should().Be("1HGCR2F83HA001234");
        record.Attributes["odometer"].Should().Be(45200);
    }

    [Fact]
    public void DetectDuplicates_IdentifiesFuzzyMatchesAndMergesRecords()
    {
        // Arrange
        var schema = _engine.CreateSchema(new CreateSchemaRequest(
            TenantId: "tenant_crm_03",
            EntityName: "Agency Partner",
            PluralName: "Agency Partners",
            Icon: "briefcase",
            Description: "Marketing agency partners",
            Fields: new List<CustomFieldDefinition>
            {
                new("f1", "agencyName", "Agency Name", FieldDataType.Text, true),
                new("f2", "domain", "Domain", FieldDataType.Text, false)
            }
        ));

        // Insert two records with slight typos
        var r1 = _engine.CreateRecord(new CreateRecordRequest("tenant_crm_03", schema.SchemaId, new Dictionary<string, object>
        {
            ["agencyName"] = "Apex Growth Digital LLC",
            ["domain"] = "apexgrowth.io"
        }));

        var r2 = _engine.CreateRecord(new CreateRecordRequest("tenant_crm_03", schema.SchemaId, new Dictionary<string, object>
        {
            ["agencyName"] = "Apex Growth Digital Inc",
            ["domain"] = "apexgrowth.com"
        }));

        // Act 1: Detect duplicates
        var duplicates = _engine.DetectDuplicates("tenant_crm_03", schema.SchemaId, "agencyName");

        // Assert 1
        duplicates.Should().HaveCount(1);
        duplicates[0].SimilarityScore.Should().BeGreaterThan(0.75);

        // Act 2: Merge r2 into r1 with unified domain
        var merged = _engine.MergeRecords(new MergeRecordsRequest(
            TenantId: "tenant_crm_03",
            SchemaId: schema.SchemaId,
            MasterRecordId: r1.RecordId,
            DuplicateRecordId: r2.RecordId,
            ResolvedAttributes: new Dictionary<string, object>
            {
                ["agencyName"] = "Apex Growth Digital Inc & LLC",
                ["domain"] = "apexgrowth.io"
            }
        ));

        // Assert 2
        merged.Attributes["agencyName"].Should().Be("Apex Growth Digital Inc & LLC");
        var allRecords = _engine.ListRecords("tenant_crm_03", schema.SchemaId);
        allRecords.Should().HaveCount(1);
    }
}
