using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Xunit;

namespace SGrowing.Tests.Data;

public class PostgreSqlSchemaAndIndexTests
{
    private static SGrowingDbContext CreateDbContext(ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options, tenantContext);
    }

    [Fact]
    public void IdentitySchema_EntitiesShouldMapToIdentitySchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(User))!.GetSchema().Should().Be("identity");
        model.FindEntityType(typeof(SystemRole))!.GetSchema().Should().Be("identity");
        model.FindEntityType(typeof(RolePermissionClaim))!.GetSchema().Should().Be("identity");
        model.FindEntityType(typeof(TenantApiKey))!.GetSchema().Should().Be("identity");
    }

    [Fact]
    public void TenancySchema_EntitiesShouldMapToTenancySchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(Tenant))!.GetSchema().Should().Be("tenancy");
        model.FindEntityType(typeof(Workspace))!.GetSchema().Should().Be("tenancy");
        model.FindEntityType(typeof(WilayaZone))!.GetSchema().Should().Be("tenancy");
    }

    [Fact]
    public void CrmSchema_EntitiesShouldMapToCrmSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(Contact))!.GetSchema().Should().Be("crm");
        model.FindEntityType(typeof(Company))!.GetSchema().Should().Be("crm");
        model.FindEntityType(typeof(Deal))!.GetSchema().Should().Be("crm");
        model.FindEntityType(typeof(Activity))!.GetSchema().Should().Be("crm");
        model.FindEntityType(typeof(CustomFieldDefinition))!.GetSchema().Should().Be("crm");
        model.FindEntityType(typeof(CustomFieldValueRecord))!.GetSchema().Should().Be("crm");
    }

    [Fact]
    public void SalesSchema_EntitiesShouldMapToSalesSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(QuoteProposal))!.GetSchema().Should().Be("sales");
        model.FindEntityType(typeof(QuoteLineItem))!.GetSchema().Should().Be("sales");
        model.FindEntityType(typeof(DigitalContract))!.GetSchema().Should().Be("sales");
        model.FindEntityType(typeof(SalesCommissionPlan))!.GetSchema().Should().Be("sales");
        model.FindEntityType(typeof(SalesCommissionStatement))!.GetSchema().Should().Be("sales");
    }

    [Fact]
    public void CommerceSchema_EntitiesShouldMapToCommerceSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(Product))!.GetSchema().Should().Be("commerce");
        model.FindEntityType(typeof(ProductVariant))!.GetSchema().Should().Be("commerce");
        model.FindEntityType(typeof(CodOrder))!.GetSchema().Should().Be("commerce");
        model.FindEntityType(typeof(Warehouse))!.GetSchema().Should().Be("commerce");
        model.FindEntityType(typeof(WarehouseInventory))!.GetSchema().Should().Be("commerce");
        model.FindEntityType(typeof(CartReservationLockRecord))!.GetSchema().Should().Be("commerce");
    }

    [Fact]
    public void EngagementSchema_EntitiesShouldMapToEngagementSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(Inbox))!.GetSchema().Should().Be("engagement");
        model.FindEntityType(typeof(Conversation))!.GetSchema().Should().Be("engagement");
        model.FindEntityType(typeof(Message))!.GetSchema().Should().Be("engagement");
        model.FindEntityType(typeof(AiDraft))!.GetSchema().Should().Be("engagement");
        model.FindEntityType(typeof(SenderAccount))!.GetSchema().Should().Be("engagement");
        model.FindEntityType(typeof(WhatsAppTemplate))!.GetSchema().Should().Be("engagement");
        model.FindEntityType(typeof(BroadcastCampaign))!.GetSchema().Should().Be("engagement");
    }

    [Fact]
    public void AutomationSchema_EntitiesShouldMapToAutomationSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(CustomerJourney))!.GetSchema().Should().Be("automation");
        model.FindEntityType(typeof(JourneyStep))!.GetSchema().Should().Be("automation");
        model.FindEntityType(typeof(WorkflowGraph))!.GetSchema().Should().Be("automation");
        model.FindEntityType(typeof(WorkflowDagEntity))!.GetSchema().Should().Be("automation");
        model.FindEntityType(typeof(WorkflowTelemetryTraceEntity))!.GetSchema().Should().Be("automation");
    }

    [Fact]
    public void SupportSchema_EntitiesShouldMapToSupportSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(HelpCategory))!.GetSchema().Should().Be("support");
        model.FindEntityType(typeof(HelpArticle))!.GetSchema().Should().Be("support");
        model.FindEntityType(typeof(SlaPolicy))!.GetSchema().Should().Be("support");
        model.FindEntityType(typeof(SurveyForm))!.GetSchema().Should().Be("support");
        model.FindEntityType(typeof(SurveyResponse))!.GetSchema().Should().Be("support");
    }

    [Fact]
    public void BillingSchema_EntitiesShouldMapToBillingSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(TenantSubscription))!.GetSchema().Should().Be("billing");
        model.FindEntityType(typeof(FeatureEntitlement))!.GetSchema().Should().Be("billing");
        model.FindEntityType(typeof(UsageMeterRecord))!.GetSchema().Should().Be("billing");
        model.FindEntityType(typeof(BillingInvoiceRecord))!.GetSchema().Should().Be("billing");
    }

    [Fact]
    public void AnalyticsSchema_EntitiesShouldMapToAnalyticsSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(AttributionTouchpoint))!.GetSchema().Should().Be("analytics");
        model.FindEntityType(typeof(TelemetryEvent))!.GetSchema().Should().Be("analytics");
        model.FindEntityType(typeof(VisitorSession))!.GetSchema().Should().Be("analytics");
    }

    [Fact]
    public void AiSchema_EntitiesShouldMapToAiSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        model.FindEntityType(typeof(DynamicAgentTool))!.GetSchema().Should().Be("ai");
        model.FindEntityType(typeof(AiAgentBudget))!.GetSchema().Should().Be("ai");
        model.FindEntityType(typeof(AgentMission))!.GetSchema().Should().Be("ai");
        model.FindEntityType(typeof(DocumentChunk))!.GetSchema().Should().Be("ai");
    }

    [Fact]
    public void PlatformSchema_EntitiesShouldMapToPlatformSchema()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        var entityType = model.FindEntityType(typeof(OutboxMessageEntity))!;
        entityType.GetSchema().Should().Be("platform");
        entityType.GetTableName().Should().Be("outbox_messages");

        // Verify Status + NextAttemptAtUtc index
        var statusIndex = entityType.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "Status", "NextAttemptAtUtc" }));
        statusIndex.Should().NotBeNull();

        // Verify CorrelationId index
        var corrIndex = entityType.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "CorrelationId" }));
        corrIndex.Should().NotBeNull();
    }

    [Fact]
    public void CompositeIndexes_CreatedAtAndTimestamp_ShouldBeConfigured()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        // Activity: (TenantId, CreatedAtUtc)
        var actIndex = model.FindEntityType(typeof(Activity))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "CreatedAtUtc" }));
        actIndex.Should().NotBeNull();

        // Message: (TenantId, CreatedAtUtc)
        var msgIndex = model.FindEntityType(typeof(Message))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "CreatedAtUtc" }));
        msgIndex.Should().NotBeNull();

        // TelemetryEvent: (TenantId, TimestampUtc)
        var telemIndex = model.FindEntityType(typeof(TelemetryEvent))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "TimestampUtc" }));
        telemIndex.Should().NotBeNull();

        // CustomerEvent: (TenantId, OccurredAtUtc)
        var custEvtIndex = model.FindEntityType(typeof(CustomerEvent))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "OccurredAtUtc" }));
        custEvtIndex.Should().NotBeNull();

        // OutboxMessageEntity: (TenantId, CreatedAtUtc)
        var outboxCreatedIndex = model.FindEntityType(typeof(OutboxMessageEntity))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "CreatedAtUtc" }));
        outboxCreatedIndex.Should().NotBeNull();

        // DocumentChunk: (TenantId, DocumentTitle)
        var docChunkIndex = model.FindEntityType(typeof(DocumentChunk))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "DocumentTitle" }));
        docChunkIndex.Should().NotBeNull();
    }

    [Fact]
    public void CompositeIndexes_TenantIdAndStatus_ShouldBeConfigured()
    {
        using var context = CreateDbContext();
        var model = context.Model;

        // Conversation: (TenantId, Status)
        var convIndex = model.FindEntityType(typeof(Conversation))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Status" }));
        convIndex.Should().NotBeNull();

        // Deal: (TenantId, Stage)
        var dealIndex = model.FindEntityType(typeof(Deal))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Stage" }));
        dealIndex.Should().NotBeNull();

        // WorkflowExecution: (TenantId, Status)
        var wfIndex = model.FindEntityType(typeof(WorkflowExecution))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Status" }));
        wfIndex.Should().NotBeNull();

        // OutboundCampaign: (TenantId, Status)
        var campIndex = model.FindEntityType(typeof(OutboundCampaign))!.GetIndexes()
            .FirstOrDefault(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Status" }));
        campIndex.Should().NotBeNull();
    }

    [Fact]
    public async Task GlobalTenantFilter_ShouldFilterEntitiesWhenTenantIsActive()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenant1);

        using var context = CreateDbContext(tenantCtx);

        // Add contacts for both tenants
        var c1 = Contact.Create(tenant1, "Elena", "Fisher", "elena@example.com");
        var c2 = Contact.Create(tenant2, "Chloe", "Frazer", "chloe@example.com");

        context.Contacts.AddRange(c1, c2);
        await context.SaveChangesAsync();

        // With tenant1 active, only c1 should be returned
        var visibleContacts = await context.Contacts.ToListAsync();
        visibleContacts.Should().ContainSingle();
        visibleContacts[0].Id.Should().Be(c1.Id);

        // IgnoreQueryFilters should return both
        var allContacts = await context.Contacts.IgnoreQueryFilters().ToListAsync();
        allContacts.Should().HaveCount(2);
    }
}
