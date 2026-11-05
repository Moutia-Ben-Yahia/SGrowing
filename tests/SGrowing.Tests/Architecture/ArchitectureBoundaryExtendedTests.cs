using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Architecture;

public class ArchitectureBoundaryExtendedTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Theory]
    [InlineData(typeof(CashflowForecastEntity), "analytics")]
    [InlineData(typeof(TopicalClusterEntity), "engagement")]
    [InlineData(typeof(DynamicElasticityPricingEntity), "sales")]
    [InlineData(typeof(RetentionBanditArmEntity), "engagement")]
    public void HorizonPersistentEntities_AreMappedToExplicitPostgreSqlSchemas(Type entityType, string expectedSchema)
    {
        using var db = CreateInMemoryDb();
        var modelEntityType = db.Model.FindEntityType(entityType);

        modelEntityType.Should().NotBeNull($"Entity {entityType.Name} should be registered in SGrowingDbContext");
        var schema = modelEntityType!.GetSchema();

        schema.Should().NotBeNullOrWhiteSpace();
        schema.Should().Be(expectedSchema, $"Entity {entityType.Name} should reside in schema '{expectedSchema}'");
    }

    [Theory]
    [InlineData(typeof(CashflowForecastEntity))]
    [InlineData(typeof(TopicalClusterEntity))]
    [InlineData(typeof(DynamicElasticityPricingEntity))]
    [InlineData(typeof(RetentionBanditArmEntity))]
    public void HorizonPersistentEntities_HaveTenantIdAndParameterlessConstructor(Type entityType)
    {
        // 1. Must have parameterless constructor
        var ctor = entityType.GetConstructor(Type.EmptyTypes);
        ctor.Should().NotBeNull($"{entityType.Name} must have a parameterless constructor for EF Core");

        // 2. Must have TenantId property of type Guid
        var tenantProp = entityType.GetProperty("TenantId");
        tenantProp.Should().NotBeNull($"{entityType.Name} must have TenantId for multi-tenant isolation");
        tenantProp!.PropertyType.Should().Be(typeof(Guid));
    }
}
