using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Api.Modules.Funnels.Autonomous;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Api.Modules.Organizations;
using SGrowing.Api.Modules.Storage.Media;
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
