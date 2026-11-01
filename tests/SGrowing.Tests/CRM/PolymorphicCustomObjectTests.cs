using FluentAssertions;
using SGrowing.Api.Modules.CRM.CustomObjects;
using System.Text.Json;
using Xunit;

namespace SGrowing.Tests.CRM;

public class PolymorphicCustomObjectTests
{
    private readonly IEavDynamicSchemaValidator _validator = new EavDynamicSchemaValidator();
    private readonly IDynamicCustomObjectQueryBuilder _queryBuilder = new DynamicCustomObjectQueryBuilder();
    private readonly DynamicCustomObjectService _service;
    private readonly Guid _tenantId = Guid.NewGuid();

    public PolymorphicCustomObjectTests()
    {
        _service = new DynamicCustomObjectService(_validator, _queryBuilder);
    }

    private static CreateCustomObjectSchemaRequest CreateVehicleSchemaRequest(string slug = "vehicles") =>
        new(
            ObjectSlug: slug,
            SingularName: "Vehicle",
            PluralName: "Vehicles",
            Description: "Fleet and inventory vehicles tracking",
            Fields:
            [
                new CustomFieldDefinitionDto("vin", "Vehicle Identification Number", CustomObjectFieldType.Text, IsRequired: true, ValidationRegex: @"^[A-HJ-NPR-Z0-9]{17}$"),
                new CustomFieldDefinitionDto("make", "Make / Brand", CustomObjectFieldType.Text, IsRequired: true),
                new CustomFieldDefinitionDto("model", "Model", CustomObjectFieldType.Text, IsRequired: true),
                new CustomFieldDefinitionDto("mileage", "Odometer Mileage", CustomObjectFieldType.Number, IsRequired: false, Min: 0, Max: 1_000_000),
                new CustomFieldDefinitionDto("price", "Listing Price", CustomObjectFieldType.Currency, IsRequired: false, Min: 500, Max: 500_000),
                new CustomFieldDefinitionDto("is_electric", "Is Electric", CustomObjectFieldType.Boolean, IsRequired: false),
                new CustomFieldDefinitionDto("manufacture_date", "Manufacture Date", CustomObjectFieldType.Date, IsRequired: false),
                new CustomFieldDefinitionDto("dealer_email", "Dealer Contact Email", CustomObjectFieldType.Email, IsRequired: false),
                new CustomFieldDefinitionDto("fuel_type", "Fuel Type", CustomObjectFieldType.Select, IsRequired: false, Options: ["Gasoline", "Diesel", "Electric", "Hybrid"]),
                new CustomFieldDefinitionDto("packages", "Installed Options", CustomObjectFieldType.MultiSelect, IsRequired: false, Options: ["Sunroof", "Leather", "Navigation", "AWD"])
            ],
            Relationships:
            [
                new CustomRelationshipDto("contacts", CustomRelationshipType.ManyToOne, "buyer"),
                new CustomRelationshipDto("warranty-claims", CustomRelationshipType.OneToMany, "claims")
            ]
        );

    #region 1. Schema Creation & Retrieval

    [Fact]
    public async Task CreateSchemaAsync_WithValidDefinition_CreatesAndStoresSchema()
    {
        // Arrange
        var req = CreateVehicleSchemaRequest();

        // Act
        var schema = await _service.CreateSchemaAsync(_tenantId, req);

        // Assert
        schema.Should().NotBeNull();
        schema.Id.Should().NotBeEmpty();
        schema.TenantId.Should().Be(_tenantId);
        schema.ObjectSlug.Should().Be("vehicles");
        schema.SingularName.Should().Be("Vehicle");
        schema.PluralName.Should().Be("Vehicles");
        schema.Fields.Should().HaveCount(10);
        schema.Relationships.Should().HaveCount(2);
        schema.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateSchemaAsync_WithAutomaticSlug_GeneratesHyphenatedSlugFromPluralName()
    {
        // Arrange
        var req = new CreateCustomObjectSchemaRequest(
            ObjectSlug: "",
            SingularName: "Real Estate Listing",
            PluralName: "Real Estate Listings"
        );

        // Act
        var schema = await _service.CreateSchemaAsync(_tenantId, req);

        // Assert
        schema.ObjectSlug.Should().Be("real-estate-listings");
    }

    [Fact]
    public async Task CreateSchemaAsync_WithRelationships_StoresTargetObjectSlugAndRelationshipType()
    {
        // Arrange
        var req = CreateVehicleSchemaRequest("fleet-cars");

        // Act
        var schema = await _service.CreateSchemaAsync(_tenantId, req);

        // Assert
        schema.Relationships.Should().ContainSingle(r => r.TargetObjectSlug == "contacts" && r.RelationshipType == CustomRelationshipType.ManyToOne);
        schema.Relationships.Should().ContainSingle(r => r.TargetObjectSlug == "warranty-claims" && r.RelationshipType == CustomRelationshipType.OneToMany);
    }

    [Fact]
    public async Task GetSchemasAsync_ReturnsOnlySchemasForRequestedTenant()
    {
        // Arrange
        var otherTenantId = Guid.NewGuid();
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("tenant1-vehicles"));
        await _service.CreateSchemaAsync(otherTenantId, CreateVehicleSchemaRequest("tenant2-vehicles"));

        // Act
        var tenant1Schemas = await _service.GetSchemasAsync(_tenantId);
        var tenant2Schemas = await _service.GetSchemasAsync(otherTenantId);

        // Assert
        tenant1Schemas.Should().ContainSingle(s => s.ObjectSlug == "tenant1-vehicles");
        tenant2Schemas.Should().ContainSingle(s => s.ObjectSlug == "tenant2-vehicles");
    }

    [Fact]
    public async Task GetSchemaBySlugAsync_WhenSlugExists_ReturnsMatchingSchemaCaseInsensitive()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("dealership-inventory"));

        // Act
        var schema = await _service.GetSchemaBySlugAsync(_tenantId, "Dealership-Inventory");

        // Assert
        schema.Should().NotBeNull();
        schema!.ObjectSlug.Should().Be("dealership-inventory");
    }

    [Fact]
    public async Task GetSchemaBySlugAsync_WhenSlugDoesNotExist_ReturnsNull()
    {
        // Act
        var schema = await _service.GetSchemaBySlugAsync(_tenantId, "non-existent-slug");

        // Assert
        schema.Should().BeNull();
    }

    #endregion

    #region 2. EAV Dynamic Schema Validation

    [Fact]
    public async Task Validation_RejectsMissingRequiredField()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var invalidData = new Dictionary<string, object?>
        {
            ["make"] = "Honda",
            ["model"] = "Accord"
            // Missing required 'vin'
        };

        // Act
        var result = _validator.Validate(schema, invalidData);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "vin" && e.ErrorCode == "REQUIRED_FIELD_MISSING");
    }

    [Fact]
    public async Task Validation_PassesWhenRequiredFieldsAreProvided()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var validData = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord"
        };

        // Act
        var result = _validator.Validate(schema, validData);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validation_RejectsInvalidNumber_WhenBelowMinimum()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["mileage"] = -10 // Minimum is 0
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "mileage" && e.ErrorCode == "NUMBER_MIN_VIOLATION");
    }

    [Fact]
    public async Task Validation_RejectsInvalidNumber_WhenAboveMaximum()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["mileage"] = 1_500_000 // Max is 1,000,000
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "mileage" && e.ErrorCode == "NUMBER_MAX_VIOLATION");
    }

    [Fact]
    public async Task Validation_PassesWhenNumber_IsWithinMinMaxRange()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["mileage"] = 45000
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validation_RejectsInvalidCurrency_WhenBelowMinOrInvalidType()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["price"] = 250m // Min is 500
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "price" && e.ErrorCode == "CURRENCY_MIN_VIOLATION");
    }

    [Fact]
    public async Task Validation_RejectsInvalidRegexPattern_ForVinNumber()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "BAD-VIN-12345", // Fails 17-char alphanumeric VIN regex
            ["make"] = "Honda",
            ["model"] = "Accord"
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "vin" && e.ErrorCode == "REGEX_PATTERN_MISMATCH");
    }

    [Fact]
    public async Task Validation_PassesWhenRegexPatternMatches()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA123456",
            ["make"] = "Honda",
            ["model"] = "Accord"
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validation_RejectsInvalidSelectOption()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["fuel_type"] = "Hydrogen" // Not in Gasoline, Diesel, Electric, Hybrid
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "fuel_type" && e.ErrorCode == "INVALID_SELECT_OPTION");
    }

    [Fact]
    public async Task Validation_PassesValidSelectOption_CaseInsensitive()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Tesla",
            ["model"] = "Model 3",
            ["fuel_type"] = "electric" // matches Electric
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validation_RejectsInvalidMultiSelectOption()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["packages"] = new List<string> { "Sunroof", "RocketBoosters" } // RocketBoosters invalid
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "packages" && e.ErrorCode == "INVALID_MULTISELECT_OPTION");
    }

    [Fact]
    public async Task Validation_PassesValidMultiSelectOption()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["packages"] = new List<string> { "Sunroof", "Leather" }
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validation_RejectsInvalidEmailFormat()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["dealer_email"] = "not-an-email"
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "dealer_email" && e.ErrorCode == "INVALID_EMAIL_FORMAT");
    }

    [Fact]
    public async Task Validation_PassesValidEmailFormat()
    {
        // Arrange
        var schema = await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA000001",
            ["make"] = "Honda",
            ["model"] = "Accord",
            ["dealer_email"] = "dealer@example.com"
        };

        // Act
        var result = _validator.Validate(schema, data);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region 3. Record Creation & Storage

    [Fact]
    public async Task CreateRecordAsync_ValidPayload_StoresRecordAndSerializesJsonb()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var data = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA999999",
            ["make"] = "Toyota",
            ["model"] = "Camry",
            ["mileage"] = 12500,
            ["price"] = 28000m,
            ["is_electric"] = false,
            ["fuel_type"] = "Gasoline"
        };

        // Act
        var record = await _service.CreateRecordAsync(_tenantId, "vehicles", data);

        // Assert
        record.Should().NotBeNull();
        record.Id.Should().NotBeEmpty();
        record.TenantId.Should().Be(_tenantId);
        record.ObjectSlug.Should().Be("vehicles");
        record.Data["make"].Should().Be("Toyota");
        record.DataJson.Should().Contain("1HGCR2F83HA999999");
        record.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateRecordAsync_InvalidPayload_ThrowsCustomObjectValidationException()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest());
        var invalidData = new Dictionary<string, object?>
        {
            ["make"] = "Toyota"
            // Missing VIN
        };

        // Act
        var act = async () => await _service.CreateRecordAsync(_tenantId, "vehicles", invalidData);

        // Assert
        await act.Should().ThrowAsync<CustomObjectValidationException>()
            .Where(ex => ex.Errors.Any(e => e.Field == "vin"));
    }

    #endregion

    #region 4. Dynamic Querying, Filtering, Sorting & Pagination

    [Fact]
    public async Task GetRecordsAsync_FiltersByEqualsOperator()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("query-equals-vehicles"));
        await _service.CreateRecordAsync(_tenantId, "query-equals-vehicles", new() { ["vin"] = "1HGCR2F83HA111111", ["make"] = "Tesla", ["model"] = "Model 3" });
        await _service.CreateRecordAsync(_tenantId, "query-equals-vehicles", new() { ["vin"] = "1HGCR2F83HA222222", ["make"] = "BMW", ["model"] = "M3" });

        var options = new CustomObjectQueryOptions(
            Filters: [new CustomObjectFilter("make", CustomObjectFilterOperator.Equals, "Tesla")]
        );

        // Act
        var paged = await _service.GetRecordsAsync(_tenantId, "query-equals-vehicles", options);

        // Assert
        paged.Items.Should().ContainSingle();
        paged.Items[0].Data["make"].Should().Be("Tesla");
    }

    [Fact]
    public async Task GetRecordsAsync_FiltersByGreaterThanOperator()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("query-gt-vehicles"));
        await _service.CreateRecordAsync(_tenantId, "query-gt-vehicles", new() { ["vin"] = "1HGCR2F83HA100001", ["make"] = "Ford", ["model"] = "F150", ["mileage"] = 30000 });
        await _service.CreateRecordAsync(_tenantId, "query-gt-vehicles", new() { ["vin"] = "1HGCR2F83HA100002", ["make"] = "Chevy", ["model"] = "Silverado", ["mileage"] = 85000 });

        var options = new CustomObjectQueryOptions(
            Filters: [new CustomObjectFilter("mileage", CustomObjectFilterOperator.GreaterThan, 50000)]
        );

        // Act
        var paged = await _service.GetRecordsAsync(_tenantId, "query-gt-vehicles", options);

        // Assert
        paged.Items.Should().ContainSingle();
        paged.Items[0].Data["model"].Should().Be("Silverado");
    }

    [Fact]
    public async Task GetRecordsAsync_FiltersByContainsOperator()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("query-contains-vehicles"));
        await _service.CreateRecordAsync(_tenantId, "query-contains-vehicles", new() { ["vin"] = "1HGCR2F83HA000101", ["make"] = "Tesla", ["model"] = "Model S Plaid" });
        await _service.CreateRecordAsync(_tenantId, "query-contains-vehicles", new() { ["vin"] = "1HGCR2F83HA000102", ["make"] = "Tesla", ["model"] = "Cybertruck" });

        var options = new CustomObjectQueryOptions(
            Filters: [new CustomObjectFilter("model", CustomObjectFilterOperator.Contains, "Model")]
        );

        // Act
        var paged = await _service.GetRecordsAsync(_tenantId, "query-contains-vehicles", options);

        // Assert
        paged.Items.Should().ContainSingle();
        paged.Items[0].Data["model"].Should().Be("Model S Plaid");
    }

    [Fact]
    public async Task GetRecordsAsync_FiltersByStartsWithOperator()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("query-startswith-vehicles"));
        await _service.CreateRecordAsync(_tenantId, "query-startswith-vehicles", new() { ["vin"] = "1HGCR2F83HA000001", ["make"] = "Honda", ["model"] = "Civic" });
        await _service.CreateRecordAsync(_tenantId, "query-startswith-vehicles", new() { ["vin"] = "2T1BURHE5KC000002", ["make"] = "Toyota", ["model"] = "Corolla" });

        var options = new CustomObjectQueryOptions(
            Filters: [new CustomObjectFilter("vin", CustomObjectFilterOperator.StartsWith, "1HG")]
        );

        // Act
        var paged = await _service.GetRecordsAsync(_tenantId, "query-startswith-vehicles", options);

        // Assert
        paged.Items.Should().ContainSingle();
        paged.Items[0].Data["make"].Should().Be("Honda");
    }

    [Fact]
    public async Task GetRecordsAsync_FiltersByInOperator()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("query-in-vehicles"));
        await _service.CreateRecordAsync(_tenantId, "query-in-vehicles", new() { ["vin"] = "1HGCR2F83HA000001", ["make"] = "Porsche", ["model"] = "Taycan", ["fuel_type"] = "Electric" });
        await _service.CreateRecordAsync(_tenantId, "query-in-vehicles", new() { ["vin"] = "1HGCR2F83HA000002", ["make"] = "Toyota", ["model"] = "Prius", ["fuel_type"] = "Hybrid" });
        await _service.CreateRecordAsync(_tenantId, "query-in-vehicles", new() { ["vin"] = "1HGCR2F83HA000003", ["make"] = "Dodge", ["model"] = "Ram", ["fuel_type"] = "Diesel" });

        var options = new CustomObjectQueryOptions(
            Filters: [new CustomObjectFilter("fuel_type", CustomObjectFilterOperator.In, "Electric, Hybrid")]
        );

        // Act
        var paged = await _service.GetRecordsAsync(_tenantId, "query-in-vehicles", options);

        // Assert
        paged.Items.Should().HaveCount(2);
        paged.Items.Select(i => i.Data["make"]).Should().Contain(["Porsche", "Toyota"]);
    }

    [Fact]
    public async Task GetRecordsAsync_DynamicSorting_AscendingAndDescending()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("query-sort-vehicles"));
        await _service.CreateRecordAsync(_tenantId, "query-sort-vehicles", new() { ["vin"] = "1HGCR2F83HA000001", ["make"] = "Acura", ["model"] = "TLX", ["mileage"] = 80000 });
        await _service.CreateRecordAsync(_tenantId, "query-sort-vehicles", new() { ["vin"] = "1HGCR2F83HA000002", ["make"] = "Audi", ["model"] = "A4", ["mileage"] = 15000 });
        await _service.CreateRecordAsync(_tenantId, "query-sort-vehicles", new() { ["vin"] = "1HGCR2F83HA000003", ["make"] = "BMW", ["model"] = "330i", ["mileage"] = 45000 });

        // Act - Ascending
        var ascResult = await _service.GetRecordsAsync(_tenantId, "query-sort-vehicles", new CustomObjectQueryOptions(SortBy: "mileage", SortDescending: false));
        // Act - Descending
        var descResult = await _service.GetRecordsAsync(_tenantId, "query-sort-vehicles", new CustomObjectQueryOptions(SortBy: "mileage", SortDescending: true));

        // Assert
        ascResult.Items[0].Data["make"].Should().Be("Audi"); // 15k
        ascResult.Items[2].Data["make"].Should().Be("Acura"); // 80k

        descResult.Items[0].Data["make"].Should().Be("Acura"); // 80k
        descResult.Items[2].Data["make"].Should().Be("Audi"); // 15k
    }

    [Fact]
    public async Task GetRecordsAsync_Pagination_ReturnsCorrectPageAndPageSize()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("query-page-vehicles"));
        for (var i = 1; i <= 15; i++)
        {
            var vinNum = i.ToString().PadLeft(6, '0');
            await _service.CreateRecordAsync(_tenantId, "query-page-vehicles", new()
            {
                ["vin"] = $"1HGCR2F83HA{vinNum}",
                ["make"] = "Brand" + i,
                ["model"] = "Model" + i
            });
        }

        var options = new CustomObjectQueryOptions(Page: 2, PageSize: 5);

        // Act
        var result = await _service.GetRecordsAsync(_tenantId, "query-page-vehicles", options);

        // Assert
        result.TotalCount.Should().Be(15);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(5);
    }

    #endregion

    #region 5. Record Updates, Partial Patching & Deletions

    [Fact]
    public async Task UpdateRecordAsync_FullReplacement_UpdatesData()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("update-vehicles"));
        var record = await _service.CreateRecordAsync(_tenantId, "update-vehicles", new()
        {
            ["vin"] = "1HGCR2F83HA123456",
            ["make"] = "Ford",
            ["model"] = "Mustang",
            ["mileage"] = 10000
        });

        var replacementData = new Dictionary<string, object?>
        {
            ["vin"] = "1HGCR2F83HA123456",
            ["make"] = "Ford",
            ["model"] = "Mustang GT",
            ["mileage"] = 25000,
            ["price"] = 45000m
        };

        // Act
        var updated = await _service.UpdateRecordAsync(_tenantId, "update-vehicles", record.Id, replacementData, partialUpdate: false);

        // Assert
        updated.Data["model"].Should().Be("Mustang GT");
        Convert.ToDecimal(updated.Data["mileage"]).Should().Be(25000m);
        updated.UpdatedAtUtc.Should().BeAfter(record.CreatedAtUtc.AddMilliseconds(-100));
    }

    [Fact]
    public async Task UpdateRecordAsync_PartialPatch_MergesFieldsWithoutLosingExistingData()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("patch-vehicles"));
        var record = await _service.CreateRecordAsync(_tenantId, "patch-vehicles", new()
        {
            ["vin"] = "1HGCR2F83HA123456",
            ["make"] = "Ford",
            ["model"] = "Focus",
            ["mileage"] = 50000
        });

        // Patch only the mileage
        var patchData = new Dictionary<string, object?>
        {
            ["mileage"] = 55000
        };

        // Act
        var updated = await _service.UpdateRecordAsync(_tenantId, "patch-vehicles", record.Id, patchData, partialUpdate: true);

        // Assert
        updated.Data["vin"].Should().Be("1HGCR2F83HA123456");
        updated.Data["make"].Should().Be("Ford");
        updated.Data["model"].Should().Be("Focus");
        Convert.ToDecimal(updated.Data["mileage"]).Should().Be(55000m);
    }

    [Fact]
    public async Task DeleteRecordAsync_RemovesRecordSuccessfully()
    {
        // Arrange
        await _service.CreateSchemaAsync(_tenantId, CreateVehicleSchemaRequest("delete-vehicles"));
        var record = await _service.CreateRecordAsync(_tenantId, "delete-vehicles", new()
        {
            ["vin"] = "1HGCR2F83HA123456",
            ["make"] = "Volvo",
            ["model"] = "XC90"
        });

        // Act
        var deleteResult = await _service.DeleteRecordAsync(_tenantId, "delete-vehicles", record.Id);
        var fetchAfterDelete = await _service.GetRecordByIdAsync(_tenantId, "delete-vehicles", record.Id);

        // Assert
        deleteResult.Should().BeTrue();
        fetchAfterDelete.Should().BeNull();
    }

    [Fact]
    public void QueryBuilder_BuildPostgresJsonbWhereClause_GeneratesValidPostgresSql()
    {
        // Arrange
        List<CustomObjectFilter> filters =
        [
            new("vin", CustomObjectFilterOperator.Equals, "1HGCR2F83HA123456"),
            new("mileage", CustomObjectFilterOperator.GreaterThan, 50000),
            new("fuel_type", CustomObjectFilterOperator.In, "Electric,Hybrid")
        ];

        // Act
        var sql = _queryBuilder.BuildPostgresJsonbWhereClause("data", filters);

        // Assert
        sql.Should().Contain("(data->>'vin') = '1HGCR2F83HA123456'");
        sql.Should().Contain("((data->>'mileage')::numeric > 50000)");
        sql.Should().Contain("(data->>'fuel_type') IN ('Electric', 'Hybrid')");
        sql.Should().Contain(" AND ");
    }

    #endregion
}
