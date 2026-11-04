using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.AI;

public class AgentPersonaServiceTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task GetPersonasAsync_SeedsAndReturnsPersonasByLayer()
    {
        using var db = CreateDbContext();
        var service = new AgentPersonaService(db);

        // Fetch all
        var allPersonas = (await service.GetPersonasAsync()).ToList();
        allPersonas.Should().NotBeEmpty();
        allPersonas.Should().Contain(p => p.RoleKey == "revops-strategist");
        allPersonas.Should().Contain(p => p.RoleKey == "logistics-controller");

        // Filter by Business
        var businessPersonas = (await service.GetPersonasAsync(layer: "Business")).ToList();
        businessPersonas.Should().NotBeEmpty();
        businessPersonas.Should().OnlyContain(p => p.Layer.Equals("Business", StringComparison.OrdinalIgnoreCase));

        // Filter by Engineering
        var engineeringPersonas = (await service.GetPersonasAsync(layer: "Engineering")).ToList();
        engineeringPersonas.Should().NotBeEmpty();
        engineeringPersonas.Should().OnlyContain(p => p.Layer.Equals("Engineering", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecutePersonaTaskAsync_ExecutesTaskAndReturnsValidResult()
    {
        using var db = CreateDbContext();
        var service = new AgentPersonaService(db);
        var tenantId = Guid.NewGuid();

        var request = new RunPersonaTaskRequest("Analyze monthly pipeline performance and flag bottlenecks.");

        var result = await service.ExecutePersonaTaskAsync(tenantId, "revops-strategist", request);

        result.Should().NotBeNull();
        result!.RoleKey.Should().Be("revops-strategist");
        result.AgentName.Should().Be("RevOps Strategist");
        result.TokensUsed.Should().BeGreaterThan(0);
        result.LatencyMs.Should().BeGreaterThan(0);
        result.Output.Should().Contain("Analyze monthly pipeline performance");

        // Verify status was reverted back to Active
        var persona = await db.AgentPersonas.FirstAsync(p => p.RoleKey == "revops-strategist");
        persona.Status.Should().Be("Active");
        persona.LastHeartbeatUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecutePersonaTaskAsync_WhenRoleNotFound_ReturnsNull()
    {
        using var db = CreateDbContext();
        var service = new AgentPersonaService(db);

        var result = await service.ExecutePersonaTaskAsync(Guid.NewGuid(), "non_existent_role", new RunPersonaTaskRequest("test"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task RunHeartbeatCycleAsync_EvaluatesAnomaliesAndGeneratesReports()
    {
        using var db = CreateDbContext();
        var service = new AgentPersonaService(db);
        var tenantId = Guid.NewGuid();

        var cycleResult = await service.RunHeartbeatCycleAsync(tenantId);

        cycleResult.Should().NotBeNull();
        cycleResult.TenantId.Should().Be(tenantId);
        cycleResult.AgentsEvaluatedCount.Should().BeGreaterThan(0);
        cycleResult.Reports.Should().NotBeEmpty();
        cycleResult.Reports.Should().Contain(r => r.RoleKey == "revops-strategist");
        cycleResult.Reports.Should().Contain(r => r.RoleKey == "logistics-controller");
    }

    [Fact]
    public async Task ExecuteAutomatedRemediationAsync_WhenAnomaliesExist_ExecutesRemediationsSuccessfully()
    {
        using var db = CreateDbContext();
        var service = new AgentPersonaService(db);
        var tenantId = Guid.NewGuid();

        // Seed stock shortage (QuantityAvailable < 50)
        var shortage = new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = Guid.NewGuid(),
            Sku = "SKU-SHORTAGE-001",
            QuantityAvailable = 15,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.WarehouseStockRecords.Add(shortage);

        // Seed low margin deal (< 25%)
        var lowMarginDeal = new DealMarginRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DealId = Guid.NewGuid(),
            Revenue = 1000m,
            CostOfGoodsSold = 600m,
            CourierShippingFees = 200m,
            OperatorLaborCost = 100m,
            TrueGrossMargin = 100m,
            GrossMarginPercentage = 10m,
            CalculatedAtUtc = DateTime.UtcNow
        };
        db.DealMarginRecords.Add(lowMarginDeal);

        await db.SaveChangesAsync();

        var remediations = await service.ExecuteAutomatedRemediationAsync(tenantId);

        remediations.Should().NotBeEmpty();
        remediations.Should().OnlyContain(r => r.Success == true);
        remediations.Should().Contain(r => r.RoleKey == "logistics-controller");
        remediations.Should().Contain(r => r.RoleKey == "revops-strategist");

        // Verify stock was replenished (+100 units)
        var updatedStock = await db.WarehouseStockRecords.FindAsync(shortage.Id);
        updatedStock!.QuantityAvailable.Should().Be(115);
    }

    [Fact]
    public async Task ExecuteAutomatedRemediationAsync_WhenNoAnomalies_ReturnsEmptyList()
    {
        using var db = CreateDbContext();
        var service = new AgentPersonaService(db);
        var tenantId = Guid.NewGuid();

        var remediations = await service.ExecuteAutomatedRemediationAsync(tenantId);

        remediations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPersonasAsync_WhenLayerMatchesNoPersonas_ReturnsEmptyCollection()
    {
        using var db = CreateDbContext();
        var service = new AgentPersonaService(db);

        var results = await service.GetPersonasAsync(layer: "NonExistentSpaceLayer");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task RunHeartbeatCycleAsync_WithUrgentBlockedTasks_GeneratesWarningForDeliveryScrumMaster()
    {
        using var db = CreateDbContext();
        var service = new AgentPersonaService(db);
        var tenantId = Guid.NewGuid();

        var urgentTask = new TaskItemRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            TenantId = tenantId,
            Title = "Contract Security Clearance",
            Priority = "Urgent",
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.TaskItems.Add(urgentTask);
        await db.SaveChangesAsync();

        var cycleResult = await service.RunHeartbeatCycleAsync(tenantId);

        cycleResult.Should().NotBeNull();
        var scrumMasterReport = cycleResult.Reports.FirstOrDefault(r => r.RoleKey == "delivery-scrum-master");
        scrumMasterReport.Should().NotBeNull();
        scrumMasterReport!.Status.Should().Be("Warning");
        scrumMasterReport.FindingsSummary.Should().Contain("1 urgent client onboarding milestone task(s)");
    }
}
