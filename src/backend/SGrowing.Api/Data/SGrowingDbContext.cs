using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Data;

public class SGrowingDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public SGrowingDbContext(DbContextOptions<SGrowingDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Inbox> Inboxes => Set<Inbox>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<AiDraft> AiDrafts => Set<AiDraft>();
    public DbSet<SenderAccount> SenderAccounts => Set<SenderAccount>();
    public DbSet<WarmupRecord> WarmupRecords => Set<WarmupRecord>();
    public DbSet<OutboundCampaign> OutboundCampaigns => Set<OutboundCampaign>();
    public DbSet<CadenceStep> CadenceSteps => Set<CadenceStep>();
    public DbSet<ProspectEnrollment> ProspectEnrollments => Set<ProspectEnrollment>();
    public DbSet<Deal> Deals => Set<Deal>();

    // Customer Journeys (Phase 3)
    public DbSet<CustomerEvent> CustomerEvents => Set<CustomerEvent>();
    public DbSet<CustomerJourney> CustomerJourneys => Set<CustomerJourney>();
    public DbSet<JourneyStep> JourneySteps => Set<JourneyStep>();
    public DbSet<JourneyEnrollment> JourneyEnrollments => Set<JourneyEnrollment>();

    // Feedback & CSAT (Phase 4)
    public DbSet<SurveyForm> SurveyForms => Set<SurveyForm>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();

    // Knowledge Base & Help Center (Phase 5)
    public DbSet<HelpCategory> HelpCategories => Set<HelpCategory>();
    public DbSet<HelpArticle> HelpArticles => Set<HelpArticle>();

    // WhatsApp Templates & Audience Broadcasts (Pillar C)
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<BroadcastCampaign> BroadcastCampaigns => Set<BroadcastCampaign>();

    // Booking & In-Chat Checkout (Pillar D)
    public DbSet<AppointmentSlot> AppointmentSlots => Set<AppointmentSlot>();
    public DbSet<AppointmentBooking> AppointmentBookings => Set<AppointmentBooking>();
    public DbSet<CheckoutLink> CheckoutLinks => Set<CheckoutLink>();

    // SLA Policies (Pillar E)
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    // Voice & Telephony
    public DbSet<VoiceCallSession> VoiceCallSessions => Set<VoiceCallSession>();

    // Social Publishing
    public DbSet<SocialAccount> SocialAccounts => Set<SocialAccount>();
    public DbSet<SocialPost> SocialPosts => Set<SocialPost>();

    // Multi-Touch AI SDR
    public DbSet<CadenceTouchpoint> CadenceTouchpoints => Set<CadenceTouchpoint>();
    public DbSet<TouchpointExecution> TouchpointExecutions => Set<TouchpointExecution>();

    // Workflows & Visual Automation
    public DbSet<WorkflowGraph> WorkflowGraphs => Set<WorkflowGraph>();
    public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();

    // Enterprise Horizons (Voice Streaming, OAuth Vault, pgvector RAG, Contracts & Enrichment)
    public DbSet<StreamingVoiceCallSession> StreamingVoiceCallSessions => Set<StreamingVoiceCallSession>();
    public DbSet<ChannelCredential> ChannelCredentials => Set<ChannelCredential>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<DigitalContract> DigitalContracts => Set<DigitalContract>();
    public DbSet<LeadEnrichmentProfile> LeadEnrichmentProfiles => Set<LeadEnrichmentProfile>();

    // Global Commercial Supremacy (PIM Catalog, CPQ, Storefront Mesh, Webhook Mesh, Privacy)
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<QuoteProposal> QuoteProposals => Set<QuoteProposal>();
    public DbSet<QuoteLineItem> QuoteLineItems => Set<QuoteLineItem>();
    public DbSet<StorefrontConnection> StorefrontConnections => Set<StorefrontConnection>();
    public DbSet<AbandonedCartRecord> AbandonedCartRecords => Set<AbandonedCartRecord>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();
    public DbSet<PrivacyErasureRequest> PrivacyErasureRequests => Set<PrivacyErasureRequest>();

    // Global Strategic Leadership (Revenue Attribution, SaaS Metering, Cron DLQ, AI Gateway)
    public DbSet<AttributionTouchpoint> AttributionTouchpoints => Set<AttributionTouchpoint>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<FeatureEntitlement> FeatureEntitlements => Set<FeatureEntitlement>();
    public DbSet<UsageMeterRecord> UsageMeterRecords => Set<UsageMeterRecord>();
    public DbSet<QuietHoursPolicy> QuietHoursPolicies => Set<QuietHoursPolicy>();
    public DbSet<WorkflowDeadLetterLog> WorkflowDeadLetterLogs => Set<WorkflowDeadLetterLog>();
    public DbSet<DynamicAgentTool> DynamicAgentTools => Set<DynamicAgentTool>();

    // Omnichannel Commerce, Logistics, Defensive WMS, Identity & Cadences
    public DbSet<CodOrder> CodOrders => Set<CodOrder>();
    public DbSet<OrderCollisionLock> OrderCollisionLocks => Set<OrderCollisionLock>();
    public DbSet<CallOutcomeRecord> CallOutcomeRecords => Set<CallOutcomeRecord>();
    public DbSet<CourierProvider> CourierProviders => Set<CourierProvider>();
    public DbSet<ShipmentRecord> ShipmentRecords => Set<ShipmentRecord>();
    public DbSet<ShippingManifest> ShippingManifests => Set<ShippingManifest>();
    public DbSet<BlacklistEntry> BlacklistEntries => Set<BlacklistEntry>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseInventory> WarehouseInventories => Set<WarehouseInventory>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<UnifiedCustomerProfile> UnifiedCustomerProfiles => Set<UnifiedCustomerProfile>();
    public DbSet<CustomerIdentityLink> CustomerIdentityLinks => Set<CustomerIdentityLink>();
    public DbSet<CustomerTimelineEvent> CustomerTimelineEvents => Set<CustomerTimelineEvent>();
    public DbSet<ReturnMerchandiseAuthorization> ReturnMerchandiseAuthorizations => Set<ReturnMerchandiseAuthorization>();
    public DbSet<SalesOutreachCadence> SalesOutreachCadences => Set<SalesOutreachCadence>();
    public DbSet<SalesCadenceStepItem> SalesCadenceStepItems => Set<SalesCadenceStepItem>();
    public DbSet<SalesCadenceLeadEnrollment> SalesCadenceLeadEnrollments => Set<SalesCadenceLeadEnrollment>();
    public DbSet<SdrTaskItem> SdrTaskItems => Set<SdrTaskItem>();

    // Ad Conversions, Remittance Clearing, In-Chat Commerce & Storefront Sync
    public DbSet<ServerConversionEvent> ServerConversionEvents => Set<ServerConversionEvent>();
    public DbSet<ConversionTrackingConfig> ConversionTrackingConfigs => Set<ConversionTrackingConfig>();
    public DbSet<CourierRemittanceStatement> CourierRemittanceStatements => Set<CourierRemittanceStatement>();
    public DbSet<RemittanceItemMatch> RemittanceItemMatches => Set<RemittanceItemMatch>();
    public DbSet<InChatCartSession> InChatCartSessions => Set<InChatCartSession>();
    public DbSet<ExternalStoreSyncRecord> ExternalStoreSyncRecords => Set<ExternalStoreSyncRecord>();
    public DbSet<GoogleSheetsSyncConfig> GoogleSheetsSyncConfigs => Set<GoogleSheetsSyncConfig>();

    // Autonomous Fulfillment & Operations Expansion
    public DbSet<VoiceConfirmationCampaign> VoiceConfirmationCampaigns => Set<VoiceConfirmationCampaign>();
    public DbSet<OutboundCallAttempt> OutboundCallAttempts => Set<OutboundCallAttempt>();
    public DbSet<WarehousePickSlip> WarehousePickSlips => Set<WarehousePickSlip>();
    public DbSet<PickSlipItem> PickSlipItems => Set<PickSlipItem>();
    public DbSet<CourierManifestRunSheet> CourierManifestRunSheets => Set<CourierManifestRunSheet>();
    public DbSet<ManifestParcelItem> ManifestParcelItems => Set<ManifestParcelItem>();
    public DbSet<WarehouseRmaInspection> WarehouseRmaInspections => Set<WarehouseRmaInspection>();
    public DbSet<MetaCatalogItem> MetaCatalogItems => Set<MetaCatalogItem>();
    public DbSet<MetaCatalogSyncFeed> MetaCatalogSyncFeeds => Set<MetaCatalogSyncFeed>();
    public DbSet<StorefrontDomain> StorefrontDomains => Set<StorefrontDomain>();
    public DbSet<BrandThemeTokens> BrandThemeTokens => Set<BrandThemeTokens>();
    public DbSet<CustomerReputationScorecard> CustomerReputationScorecards => Set<CustomerReputationScorecard>();
    public DbSet<MicroDepositPaymentLink> MicroDepositPaymentLinks => Set<MicroDepositPaymentLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            b.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).IsRequired().HasMaxLength(256);
            b.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.EventType).IsRequired().HasMaxLength(100);
            b.HasIndex(o => new { o.ProcessedOnUtc, o.OccurredOnUtc });
        });

        modelBuilder.Entity<Contact>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.FirstName).HasMaxLength(100);
            b.Property(c => c.LastName).HasMaxLength(100);
            b.Property(c => c.Email).HasMaxLength(256);
            b.Property(c => c.Phone).HasMaxLength(50);
            b.HasIndex(c => new { c.TenantId, c.Email });
            b.HasIndex(c => new { c.TenantId, c.Phone });
        });

        modelBuilder.Entity<Company>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(c => new { c.TenantId, c.Domain });
        });

        modelBuilder.Entity<Activity>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => new { a.TenantId, a.ContactId, a.CreatedAtUtc });
        });

        modelBuilder.Entity<Inbox>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Name).IsRequired().HasMaxLength(100);
            b.HasIndex(i => new { i.TenantId, i.ChannelType });
        });

        modelBuilder.Entity<Conversation>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasIndex(c => new { c.TenantId, c.Status, c.LastMessageAtUtc });
            b.HasIndex(c => new { c.TenantId, c.ContactId });
        });

        modelBuilder.Entity<Message>(b =>
        {
            b.HasKey(m => m.Id);
            b.HasIndex(m => new { m.TenantId, m.ConversationId, m.CreatedAtUtc });
            b.HasIndex(m => new { m.TenantId, m.ExternalMessageId });
        });

        modelBuilder.Entity<AiDraft>(b =>
        {
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.TenantId, d.ConversationId, d.Status });
        });
    }
}
