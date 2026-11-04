using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Data;

public class GrowthOsPersistenceTests
{
    private static SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CustomerIdentityEntity_ShouldMapToCustomerDataSchema_AndEnforceCompoundIndex()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(CustomerIdentityEntity));
        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("customer_data");
        entityType.GetTableName().Should().Be("identities");

        // Verify composite unique index (TenantId, Type, Value)
        var compoundIndex = entityType.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "TenantId", "Type", "Value" }));

        compoundIndex.Should().NotBeNull();
        compoundIndex!.IsUnique.Should().BeTrue();

        // Verify primary key
        var primaryKey = entityType.FindPrimaryKey();
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Select(p => p.Name).Should().Equal(nameof(CustomerIdentityEntity.Id));

        // Verify persistence round-trip
        var identity = new CustomerIdentityEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_alpha_01",
            Type = "email",
            Value = "founder@nextgenbrands.com",
            CanonicalProfileId = "prof_lead_8801",
            CreatedAtUtc = DateTime.UtcNow
        };

        context.CustomerIdentities.Add(identity);
        await context.SaveChangesAsync();

        var saved = await context.CustomerIdentities.FindAsync(identity.Id);
        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be("tenant_alpha_01");
        saved.Type.Should().Be("email");
        saved.Value.Should().Be("founder@nextgenbrands.com");
        saved.CanonicalProfileId.Should().Be("prof_lead_8801");
    }

    [Fact]
    public async Task CustomerProfileEntity_ShouldStoreJsonbUnifiedTraits()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(CustomerProfileEntity));
        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("customer_data");
        entityType.GetTableName().Should().Be("profiles");

        // Verify jsonb column type mapping
        var unifiedTraitsProp = entityType.FindProperty(nameof(CustomerProfileEntity.UnifiedTraitsJson));
        unifiedTraitsProp.Should().NotBeNull();
        unifiedTraitsProp!.FindAnnotation("Relational:ColumnType")?.Value?.ToString().Should().Be("jsonb");

        // Verify persistence of JSONB document
        var profileId = Guid.NewGuid();
        const string unifiedTraits = """
        {
            "segment": "enterprise_buyer",
            "predicted_ltv": 15800.50,
            "churn_score": 0.04,
            "rfm": { "recency": 2, "frequency": 14, "monetary": 4820.00 },
            "custom_attributes": { "vertical": "Direct-to-Consumer", "headquarters": "Algiers" }
        }
        """;

        var profile = new CustomerProfileEntity
        {
            Id = profileId,
            TenantId = "tenant_alpha_01",
            CanonicalProfileId = "prof_lead_8801",
            UnifiedTraitsJson = unifiedTraits,
            FirstSeenUtc = DateTime.UtcNow.AddMonths(-6),
            LastActiveUtc = DateTime.UtcNow
        };

        context.CustomerProfiles.Add(profile);
        await context.SaveChangesAsync();

        var retrieved = await context.CustomerProfiles.FindAsync(profileId);
        retrieved.Should().NotBeNull();
        retrieved!.CanonicalProfileId.Should().Be("prof_lead_8801");
        retrieved.UnifiedTraitsJson.Should().Be(unifiedTraits);
        retrieved.UnifiedTraitsJson.Should().Contain("enterprise_buyer");
        retrieved.UnifiedTraitsJson.Should().Contain("Algiers");
    }

    [Fact]
    public async Task CapiConversionEntity_ShouldEnforcePlatformDeduplicationConstraints()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(CapiConversionEntity));
        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("advertising");
        entityType.GetTableName().Should().Be("conversions");

        // Verify composite unique index (TenantId, Platform, EventId) for deduplication
        var dedupeIndex = entityType.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "TenantId", "Platform", "EventId" }));

        dedupeIndex.Should().NotBeNull();
        dedupeIndex!.IsUnique.Should().BeTrue();

        // Verify persistence with deduplication hash and flag
        var conversion = new CapiConversionEntity
        {
            EventId = "evt_purchase_meta_98124",
            TenantId = "tenant_ads_meta",
            Platform = "MetaCapi",
            EventName = "Purchase",
            ConversionValue = 429.99m,
            Currency = "USD",
            PayloadHash = "sha256_e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            DispatchedAtUtc = DateTime.UtcNow,
            IsDeduplicated = true
        };

        context.CapiConversions.Add(conversion);
        await context.SaveChangesAsync();

        var saved = await context.CapiConversions.FindAsync(conversion.EventId);
        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be("tenant_ads_meta");
        saved.Platform.Should().Be("MetaCapi");
        saved.ConversionValue.Should().Be(429.99m);
        saved.Currency.Should().Be("USD");
        saved.PayloadHash.Should().Be("sha256_e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
        saved.IsDeduplicated.Should().BeTrue();
    }

    [Fact]
    public async Task WorkflowDagEntity_ShouldPreserveNodesAndEdgesJson()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(WorkflowDagEntity));
        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("automation");
        entityType.GetTableName().Should().Be("workflows");

        // Verify composite unique index (TenantId, WorkflowId)
        var workflowIndex = entityType.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "TenantId", "WorkflowId" }));

        workflowIndex.Should().NotBeNull();
        workflowIndex!.IsUnique.Should().BeTrue();

        // Verify jsonb column mappings
        entityType.FindProperty(nameof(WorkflowDagEntity.NodesJson))?.FindAnnotation("Relational:ColumnType")?.Value?.ToString().Should().Be("jsonb");
        entityType.FindProperty(nameof(WorkflowDagEntity.EdgesJson))?.FindAnnotation("Relational:ColumnType")?.Value?.ToString().Should().Be("jsonb");

        // Verify persistence of complex DAG topology
        const string nodesJson = """
        [
            {"nodeId":"node_trigger","label":"Incoming WhatsApp Message","type":"Trigger"},
            {"nodeId":"node_ai_agent","label":"LLM Intent Classifier","type":"ActionAI"},
            {"nodeId":"node_route_order","label":"Create COD Order","type":"ActionCommerce"}
        ]
        """;

        const string edgesJson = """
        [
            {"sourceNodeId":"node_trigger","targetNodeId":"node_ai_agent"},
            {"sourceNodeId":"node_ai_agent","targetNodeId":"node_route_order","condition":"intent == 'buy'"}
        ]
        """;

        var dag = new WorkflowDagEntity
        {
            WorkflowId = "wf_omnichannel_cart_recovery",
            TenantId = "tenant_alpha_01",
            Name = "Omnichannel WhatsApp Recovery DAG",
            TriggerNodeId = "node_trigger",
            NodesJson = nodesJson,
            EdgesJson = edgesJson,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.WorkflowDags.Add(dag);
        await context.SaveChangesAsync();

        var saved = await context.WorkflowDags.FindAsync(dag.WorkflowId);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Omnichannel WhatsApp Recovery DAG");
        saved.NodesJson.Should().Be(nodesJson);
        saved.EdgesJson.Should().Be(edgesJson);
        saved.NodesJson.Should().Contain("LLM Intent Classifier");
        saved.EdgesJson.Should().Contain("intent == 'buy'");
    }

    [Fact]
    public async Task WorkflowTelemetryTraceEntity_ShouldRecordExecutionLatencyAndSteps()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(WorkflowTelemetryTraceEntity));
        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("automation");
        entityType.GetTableName().Should().Be("telemetry_traces");

        // Verify jsonb column mapping
        entityType.FindProperty(nameof(WorkflowTelemetryTraceEntity.StepsJson))?.FindAnnotation("Relational:ColumnType")?.Value?.ToString().Should().Be("jsonb");

        // Verify persistence of execution latency and step telemetry
        const string stepsTelemetry = """
        [
            {"stepId":"step_01","nodeId":"node_trigger","durationMs":4,"status":"Succeeded"},
            {"stepId":"step_02","nodeId":"node_ai_agent","durationMs":185,"status":"Succeeded","tokensUsed":142},
            {"stepId":"step_03","nodeId":"node_route_order","durationMs":32,"status":"Succeeded","orderId":"COD-9821"}
        ]
        """;

        var trace = new WorkflowTelemetryTraceEntity
        {
            ExecutionId = "exec_trace_d98124_fa8",
            TenantId = "tenant_alpha_01",
            WorkflowId = "wf_omnichannel_cart_recovery",
            ContactId = "cont_corp_99",
            Status = "Completed",
            TotalDurationMs = 221,
            StepsJson = stepsTelemetry,
            ExecutedAtUtc = DateTime.UtcNow
        };

        context.WorkflowTelemetryTraces.Add(trace);
        await context.SaveChangesAsync();

        var saved = await context.WorkflowTelemetryTraces.FindAsync(trace.ExecutionId);
        saved.Should().NotBeNull();
        saved!.WorkflowId.Should().Be("wf_omnichannel_cart_recovery");
        saved.ContactId.Should().Be("cont_corp_99");
        saved.Status.Should().Be("Completed");
        saved.TotalDurationMs.Should().Be(221);
        saved.StepsJson.Should().Be(stepsTelemetry);
        saved.StepsJson.Should().Contain("tokensUsed");
        saved.StepsJson.Should().Contain("COD-9821");
    }

    [Fact]
    public async Task IdentityLinkEntity_ShouldMapToCustomerDataSchema_AndPersistLinkGraph()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(IdentityLinkEntity));
        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("customer_data");
        entityType.GetTableName().Should().Be("identity_links");

        entityType.FindProperty(nameof(IdentityLinkEntity.SourceIdentifierJson))?.FindAnnotation("Relational:ColumnType")?.Value?.ToString().Should().Be("jsonb");
        entityType.FindProperty(nameof(IdentityLinkEntity.TargetIdentifierJson))?.FindAnnotation("Relational:ColumnType")?.Value?.ToString().Should().Be("jsonb");

        var link = new IdentityLinkEntity
        {
            Id = Guid.NewGuid(),
            TenantId = "tenant_alpha_01",
            SourceIdentifierJson = "{\"type\":\"cookie\",\"value\":\"ck_anon_982\"}",
            TargetIdentifierJson = "{\"type\":\"phone\",\"value\":\"+21698765432\"}",
            ConfidenceScore = 0.98,
            MatchReason = "WhatsApp click-to-chat parameter match",
            CreatedAtUtc = DateTime.UtcNow
        };

        context.IdentityLinks.Add(link);
        await context.SaveChangesAsync();

        var retrieved = await context.IdentityLinks.FindAsync(link.Id);
        retrieved.Should().NotBeNull();
        retrieved!.TenantId.Should().Be("tenant_alpha_01");
        retrieved.ConfidenceScore.Should().Be(0.98);
        retrieved.MatchReason.Should().Be("WhatsApp click-to-chat parameter match");
        retrieved.SourceIdentifierJson.Should().Contain("ck_anon_982");
        retrieved.TargetIdentifierJson.Should().Contain("+21698765432");
    }

    [Fact]
    public async Task VisitorSessionEntity_ShouldMapToCustomerDataSchema_AndPersistUtmTracking()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(VisitorSessionEntity));
        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("customer_data");
        entityType.GetTableName().Should().Be("visitor_sessions");

        var sessionIndex = entityType.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "TenantId", "AnonymousId" }));
        sessionIndex.Should().NotBeNull();

        var session = new VisitorSessionEntity
        {
            SessionId = "sess_web_78129",
            TenantId = "tenant_alpha_01",
            AnonymousId = "anon_visitor_007",
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-25),
            EndedAtUtc = DateTime.UtcNow,
            PageViewsJson = "[{\"path\":\"/landing\",\"dwellSec\":45},{\"path\":\"/pricing\",\"dwellSec\":120}]",
            UtmParametersJson = "{\"utm_source\":\"facebook\",\"utm_campaign\":\"q4_scale\",\"utm_medium\":\"cpc\"}"
        };

        context.VisitorSessionEntities.Add(session);
        await context.SaveChangesAsync();

        var retrieved = await context.VisitorSessionEntities.FindAsync(session.SessionId);
        retrieved.Should().NotBeNull();
        retrieved!.AnonymousId.Should().Be("anon_visitor_007");
        retrieved.UtmParametersJson.Should().Contain("q4_scale");
        retrieved.PageViewsJson.Should().Contain("/pricing");
    }

    [Fact]
    public async Task CampaignRoasSnapshotEntity_ShouldMapToAdvertisingSchema_AndPreservePrecision()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(typeof(CampaignRoasSnapshotEntity));
        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("advertising");
        entityType.GetTableName().Should().Be("campaign_roas_snapshots");

        var snapshot = new CampaignRoasSnapshotEntity
        {
            Id = Guid.NewGuid(),
            CampaignId = "cmp_meta_retargeting_q4",
            TenantId = "tenant_alpha_01",
            Platform = "MetaAds",
            Spend = 1450.7500m,
            Revenue = 5820.2500m,
            CurrentRoas = 4.01m,
            RecommendedSpend = 1800.00m,
            Action = "ScaleBudget",
            RecordedAtUtc = DateTime.UtcNow
        };

        context.CampaignRoasSnapshots.Add(snapshot);
        await context.SaveChangesAsync();

        var retrieved = await context.CampaignRoasSnapshots.FindAsync(snapshot.Id);
        retrieved.Should().NotBeNull();
        retrieved!.CampaignId.Should().Be("cmp_meta_retargeting_q4");
        retrieved.Platform.Should().Be("MetaAds");
        retrieved.Spend.Should().Be(1450.7500m);
        retrieved.Revenue.Should().Be(5820.2500m);
        retrieved.CurrentRoas.Should().Be(4.01m);
        retrieved.Action.Should().Be("ScaleBudget");
    }
}
