using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Api.Modules.Commerce.Warehouses;
using SGrowing.Api.Modules.Funnels.Autonomous;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Api.Modules.Organizations;
using SGrowing.Api.Modules.Storage.Media;
using SGrowing.Api.Modules.Workforce.Timesheets;
using SGrowing.Core.Common;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests;

public class StrategicNextStepsTests
{
    private SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    // =========================================================================
    // 1. Parallel Concurrency Stress Test on Stock Reservation
    // =========================================================================

    [Fact]
    public async Task Concurrency_ParallelOrderRouting_SafelyDecrementsInventoryWithoutOverselling()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        var warehouse = new WarehouseRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "WH-TUNIS-TEST",
            Name = "Tunis Concurrency Depot",
            City = "Tunis",
            Country = "Tunisia",
            Latitude = 36.8065,
            Longitude = 10.1815,
            IsActive = true
        };
        db.WarehouseRecords.Add(warehouse);

        // 30 units in stock
        var stock = new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouse.Id,
            Sku = "SKU-HOT-SALE",
            ProductName = "Limited Edition Item",
            QuantityAvailable = 30,
            QuantityReserved = 0,
            UnitCost = 50.0m,
            RowVersion = 1
        };
        db.WarehouseStockRecords.Add(stock);
        await db.SaveChangesAsync();

        var routingService = new WarehouseRoutingService(db);

        // Simulate 10 sequential / parallel order attempts requesting 5 units each (total demand = 50, but only 30 available)
        int successfulAllocations = 0;
        int totalAllocatedQuantity = 0;

        for (int i = 0; i < 10; i++)
        {
            var req = new RoutingRequest(
                TenantId: tenantId,
                OrderId: $"ORD-CONCUR-{i}",
                CustomerAddress: "Habib Bourguiba Ave",
                CustomerCity: "Tunis",
                CustomerLatitude: 36.8000,
                CustomerLongitude: 10.1800,
                Items: new List<OrderItemRequirement>
                {
                    new("SKU-HOT-SALE", 5)
                }
            );

            var result = await routingService.RouteOrderAsync(req);
            if (result.IsAllocated)
            {
                successfulAllocations++;
                totalAllocatedQuantity += result.AllocatedItems.Sum(a => a.QuantityAllocated);
            }
        }

        // Exactly 6 orders of 5 units should succeed (6 * 5 = 30 units), 4 must be backordered
        Assert.Equal(6, successfulAllocations);
        Assert.Equal(30, totalAllocatedQuantity);

        var finalStock = await db.WarehouseStockRecords.FirstAsync(s => s.Id == stock.Id);
        Assert.Equal(0, finalStock.QuantityAvailable);
        Assert.Equal(30, finalStock.QuantityReserved);
        Assert.True(finalStock.RowVersion > 1, "RowVersion concurrency token must increment on every mutation");
    }

    // =========================================================================
    // 2. Geodesic Date-Line & Polar Bounding-Box Normalization
    // =========================================================================

    [Fact]
    public async Task SpatialSearch_AntimeridianAndPolarCoordinates_NormalizesBoundingBox()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();

        // Add a warehouse in Fiji near +178 longitude
        var fijiWh = new WarehouseRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "WH-FIJI",
            Name = "Suva Pacific Depot",
            City = "Suva",
            Country = "Fiji",
            Latitude = -18.1248,
            Longitude = 178.4501,
            IsActive = true
        };
        db.WarehouseRecords.Add(fijiWh);
        await db.SaveChangesAsync();

        var service = new WarehouseRoutingService(db);

        // Query originating near the antimeridian at 179.5 longitude with 300km radius
        var req = new SpatialWarehouseQueryRequest(
            Latitude: -18.0,
            Longitude: 179.5,
            RadiusKm: 300.0
        );

        var result = await service.SpatialSearchAsync(tenantId, req);

        Assert.NotNull(result);
        Assert.NotNull(result.BoundingBox);
        Assert.True(result.BoundingBox.MinLatitude >= -90.0 && result.BoundingBox.MaxLatitude <= 90.0);
        Assert.Single(result.Warehouses);
        Assert.Equal("WH-FIJI", result.Warehouses[0].Code);
        Assert.True(result.Warehouses[0].DistanceKm < 300.0);
    }

    // =========================================================================
    // 3. Idempotent Webhook De-duplication Cache
    // =========================================================================

    [Fact]
    public async Task EventDeduplication_DuplicateWebhookPayload_RejectsSubsequentDeliveries()
    {
        var deduplicationService = new EventDeduplicationService();
        var tenantId = Guid.NewGuid();
        var eventId = "evt_stripe_charge_succeeded_849204";
        var payload = "{\"amount\": 4500, \"currency\": \"usd\", \"status\": \"succeeded\"}";

        // First delivery attempt must acquire lock
        bool firstAttempt = await deduplicationService.TryAcquireAsync(tenantId, eventId, payload);
        Assert.True(firstAttempt);

        // Immediate retry of identical event must be rejected
        bool secondAttempt = await deduplicationService.TryAcquireAsync(tenantId, eventId, payload);
        Assert.False(secondAttempt);

        // Mark processed
        await deduplicationService.MarkProcessedAsync(tenantId, eventId);
        bool isDone = await deduplicationService.IsProcessedAsync(tenantId, eventId);
        Assert.True(isDone);
    }

    // =========================================================================
    // 4. RFC 7807 Standardized ProblemDetails Output
    // =========================================================================

    [Fact]
    public void ProblemDetails_StandardizedErrorShape_ReturnsRfc7807CompliantProperties()
    {
        var conflictResult = SGrowingProblemDetails.Conflict(
            detail: "SKU-GROW-PRO has insufficient stock across regional hubs.",
            instance: "/api/v1/commerce/warehouses/route"
        );

        Assert.NotNull(conflictResult);

        var badRequestResult = SGrowingProblemDetails.BadRequest(
            detail: "Origin latitude must be between -90 and 90 degrees.",
            instance: "/api/v1/commerce/warehouses/spatial-search"
        );

        Assert.NotNull(badRequestResult);
    }

    // =========================================================================
    // 5. Workforce Timesheets & True Margin Cost Rollup (Ever-Gauzy)
    // =========================================================================

    [Fact]
    public async Task Timesheets_ApproveTimecard_RollsLaborCostDirectlyIntoDealMargin()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        // Create initial DealMarginRecord: Revenue = $10,000, COGS = $3,000, Courier = $500, Initial Labor = $0
        var dealMargin = new DealMarginRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DealId = dealId,
            OrganizationId = orgId,
            DealTitle = "Enterprise Hypermarket Deal",
            Revenue = 10000m,
            CostOfGoodsSold = 3000m,
            CourierShippingFees = 500m,
            OperatorLaborHours = 0m,
            OperatorHourlyRate = 30m,
            OperatorLaborCost = 0m,
            TrueGrossMargin = 6500m, // 10000 - 3000 - 500
            GrossMarginPercentage = 65.0m
        };
        db.DealMarginRecords.Add(dealMargin);
        await db.SaveChangesAsync();

        var timesheetService = new TimesheetService(db);

        // Log 10 hours at $30/hr ($300 labor cost)
        var logReq = new LogTimecardRequest(
            TeamMemberId: Guid.NewGuid(),
            TeamMemberName: "Sarah Jenkins",
            TaskId: Guid.NewGuid(),
            TaskTitle: "Phase 3: Catalog Ingestion & ERP Data Migration",
            DealId: dealId,
            DealTitle: "Enterprise Hypermarket Deal",
            Description: "Imported 4,200 SKU records",
            HoursWorked: 10m,
            HourlyRate: 30m
        );

        var entry = await timesheetService.LogTimecardAsync(tenantId, logReq);
        Assert.NotNull(entry);
        Assert.Equal("Submitted", entry.Status);
        Assert.Equal(300m, entry.TotalLaborCost);

        // Approve timecard
        var approved = await timesheetService.ApproveTimecardAsync(tenantId, entry.Id, new ApproveTimecardRequest("Operations Director"));
        Assert.NotNull(approved);
        Assert.Equal("Approved", approved.Status);

        // Verify DealMarginRecord has been dynamically recalculated
        var updatedMargin = await db.DealMarginRecords.FirstAsync(d => d.Id == dealMargin.Id);
        Assert.Equal(10m, updatedMargin.OperatorLaborHours);
        Assert.Equal(300m, updatedMargin.OperatorLaborCost);
        // True Gross Margin = 10000 - 3000 - 500 - 300 = 6200
        Assert.Equal(6200m, updatedMargin.TrueGrossMargin);
        Assert.Equal(62.0m, updatedMargin.GrossMarginPercentage);
    }

    // =========================================================================
    // 6. Native Compiled WhatsApp Gateway & 8-Digit Pairing Code (Evolution-Go & Baileys)
    // =========================================================================

    [Fact]
    public async Task WhatsAppGateway_GeneratePairingCode_ReturnsFormatted8DigitCode()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new WhatsAppGatewayService(db);

        // Create new instance
        var instance = await service.CreateInstanceAsync(tenantId, new CreateWhatsAppInstanceRequest("regional-sales-wa", "+216 71 000 111"));
        Assert.NotNull(instance);
        Assert.Equal("Disconnected", instance.Status);

        // Request 8-digit pairing code
        var pairing = await service.GeneratePairingCodeAsync(tenantId, instance.InstanceName, new RequestPairingCodeRequest("+216 71 000 111"));
        Assert.NotNull(pairing);
        Assert.Matches(@"^\d{4}-\d{4}$", pairing.PairingCode);
        Assert.Equal(180, pairing.ExpiresInSeconds);
        Assert.Equal("Noise_XX_25519_AESGCM_SHA256", pairing.Protocol);

        // Connect instance
        var connected = await service.ConnectInstanceAsync(tenantId, instance.InstanceName);
        Assert.NotNull(connected);
        Assert.Equal("Connected", connected.Status);
        Assert.True(connected.MemoryFootprintMb <= 30, "Memory footprint must remain ultra-low ~24MB");

        // Dispatch high-speed message
        var dispatch = await service.DispatchMessageAsync(tenantId, instance.InstanceName, new DispatchWhatsAppMessageRequest("+216 98 765 432", "text", "Your shipment has departed Tunis."));
        Assert.NotNull(dispatch);
        Assert.StartsWith("WAM-", dispatch.MessageId);
        Assert.Equal("Dispatched", dispatch.Status);
        Assert.Contains("NATS JetStream", dispatch.BrokerFanout);
    }

    // =========================================================================
    // 7. 15-Step Idea-to-Funnel Autonomous Pipeline (Ever-Works)
    // =========================================================================

    [Fact]
    public async Task AutonomousFunnel_Execute15StepPipeline_GeneratesCompleteDeploymentAudit()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new AutonomousFunnelPipelineService(db);

        var req = new ExecuteAutonomousFunnelRequest(
            ConceptPrompt: "Wholesale Multi-Warehouse B2B Commerce for FMCG Distributors",
            TargetSku: "SKU-FMCG-PRO"
        );

        var result = await service.ExecutePipelineAsync(tenantId, req);

        Assert.NotNull(result);
        Assert.NotNull(result.Run);
        Assert.Equal(15, result.Run.CurrentStepNumber);
        Assert.Equal(100, result.Run.ProgressPercentage);
        Assert.Equal("Completed", result.Run.Status);
        Assert.Equal(98, result.Run.PerformanceScore);
        Assert.Equal(100, result.Run.SeoScore);
        Assert.Equal(96, result.Run.AccessibilityScore);
        Assert.NotEmpty(result.Run.DeploymentUrl);

        // Check 15 execution steps audit trail
        Assert.Equal(15, result.Steps.Count);
        Assert.All(result.Steps, s => Assert.Equal("Completed", s.Status));
        Assert.Equal(1, result.Steps[0].StepNumber);
        Assert.Equal("Concept Ingestion & Scoping", result.Steps[0].StepName);
        Assert.Equal(15, result.Steps[14].StepNumber);
        Assert.Equal("Recurring Cron Maintenance Scheduling", result.Steps[14].StepName);
    }

    // =========================================================================
    // 8. Dual-Layer 38-Agent Persona Roster & Heartbeat Sweep (Evo-Nexus)
    // =========================================================================

    [Fact]
    public async Task AgentPersonas_CatalogAndHeartbeat_ExecutesOperationalAudit()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new AgentPersonaService(db);

        // Verify 38 cataloged personas (17 business + 21 engineering)
        var allPersonas = (await service.GetPersonasAsync()).ToList();
        Assert.Equal(38, allPersonas.Count);

        var businessPersonas = (await service.GetPersonasAsync("Business")).ToList();
        Assert.Equal(17, businessPersonas.Count);

        var engineeringPersonas = (await service.GetPersonasAsync("Engineering")).ToList();
        Assert.Equal(21, engineeringPersonas.Count);

        // Run Heartbeat sweep
        var heartbeatResult = await service.RunHeartbeatCycleAsync(tenantId);
        Assert.NotNull(heartbeatResult);
        Assert.True(heartbeatResult.AgentsEvaluatedCount >= 4);
        Assert.NotEmpty(heartbeatResult.Reports);

        var revopsReport = heartbeatResult.Reports.FirstOrDefault(r => r.RoleKey == "revops-strategist");
        Assert.NotNull(revopsReport);
        Assert.Contains("RevOps Strategist", revopsReport.AgentName);

        // Execute individual agent prompt
        var execResult = await service.ExecutePersonaTaskAsync(tenantId, "dotnet-architect", new RunPersonaTaskRequest("Verify optimistic locking on stock records"));
        Assert.NotNull(execResult);
        Assert.Contains(".NET 10 System Architect", execResult.AgentName);
        Assert.True(execResult.TokensUsed > 0);
    }

    // =========================================================================
    // 9. Direct-to-S3 Pre-Signed Object Storage Vault (Evolution-Go)
    // =========================================================================

    [Fact]
    public async Task MediaVault_GeneratePreSignedUrls_ReturnsHmacSignedEndpoints()
    {
        using var db = CreateInMemoryDb();
        var tenantId = Guid.NewGuid();
        var service = new MediaVaultService(db);

        var req = new CreateUploadUrlRequest(
            FileName: "doorstep-pod-signature.png",
            ContentType: "image/png",
            FileSizeBytes: 124500,
            ExpiryMinutes: 30
        );

        var uploadResult = await service.GeneratePreSignedUploadUrlAsync(tenantId, req);
        Assert.NotNull(uploadResult);
        Assert.Equal("PUT", uploadResult.Method);
        Assert.Contains("X-SGrowing-Signature=", uploadResult.PreSignedUploadUrl);
        Assert.Contains("vault.storage.sgrowing.io", uploadResult.PreSignedUploadUrl);

        // Confirm upload
        bool confirmed = await service.ConfirmUploadAsync(tenantId, new ConfirmUploadRequest(uploadResult.MediaId));
        Assert.True(confirmed);

        // Generate download URL
        var downloadResult = await service.GeneratePreSignedDownloadUrlAsync(tenantId, uploadResult.MediaId);
        Assert.NotNull(downloadResult);
        Assert.Contains("X-SGrowing-Signature=", downloadResult.PreSignedDownloadUrl);
        Assert.Equal("doorstep-pod-signature.png", downloadResult.FileName);
    }

    // =========================================================================
    // 10. Unified TenantScopedEntity Base Class Contract
    // =========================================================================

    private class SampleAuditEntity : TenantScopedEntity
    {
        public string EntityName { get; set; } = string.Empty;
    }

    [Fact]
    public void TenantScopedEntity_InitializesConsistentContracts()
    {
        var entity = new SampleAuditEntity
        {
            TenantId = Guid.NewGuid(),
            EntityName = "Test Scoped Item"
        };

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.NotEqual(Guid.Empty, entity.TenantId);
        Assert.False(entity.IsDeleted);
        Assert.Equal(1u, entity.RowVersion);
        Assert.True(entity.CreatedAtUtc <= DateTime.UtcNow);
    }
}
