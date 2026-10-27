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

    // Enterprise Operations: Smart Routing, Retention LTV, Multi-Hub WMS, Live Commerce & Float
    public DbSet<CarrierPerformanceMetric> CarrierPerformanceMetrics => Set<CarrierPerformanceMetric>();
    public DbSet<CarrierRateCard> CarrierRateCards => Set<CarrierRateCard>();
    public DbSet<PostPurchaseSurvey> PostPurchaseSurveys => Set<PostPurchaseSurvey>();
    public DbSet<ProductBurnRateConfig> ProductBurnRateConfigs => Set<ProductBurnRateConfig>();
    public DbSet<ReplenishmentSchedule> ReplenishmentSchedules => Set<ReplenishmentSchedule>();
    public DbSet<RegionalHubProximityRule> RegionalHubProximityRules => Set<RegionalHubProximityRule>();
    public DbSet<InterHubStockTransfer> InterHubStockTransfers => Set<InterHubStockTransfer>();
    public DbSet<SupplierPurchaseOrder> SupplierPurchaseOrders => Set<SupplierPurchaseOrder>();
    public DbSet<LiveStreamSession> LiveStreamSessions => Set<LiveStreamSession>();
    public DbSet<LiveFlashDeal> LiveFlashDeals => Set<LiveFlashDeal>();
    public DbSet<CarrierRemittanceScheduleConfig> CarrierRemittanceScheduleConfigs => Set<CarrierRemittanceScheduleConfig>();
    public DbSet<CourierFloatValuation> CourierFloatValuations => Set<CourierFloatValuation>();

    // Creator & Influencer Commission Settlement Mesh (Frontier 1)
    public DbSet<CreatorProfile> CreatorProfiles => Set<CreatorProfile>();
    public DbSet<AffiliateReferralCode> AffiliateReferralCodes => Set<AffiliateReferralCode>();
    public DbSet<CommissionEscrowLedger> CommissionEscrowLedgers => Set<CommissionEscrowLedger>();
    public DbSet<CreatorPayoutRecord> CreatorPayoutRecords => Set<CreatorPayoutRecord>();

    // Automated Reverse Logistics & Drop-and-Swap (Frontier 2)
    public DbSet<OrderExchangeRequest> OrderExchangeRequests => Set<OrderExchangeRequest>();
    public DbSet<ExchangeCourierSwapManifest> ExchangeCourierSwapManifests => Set<ExchangeCourierSwapManifest>();
    public DbSet<ItemReturnInspection> ItemReturnInspections => Set<ItemReturnInspection>();

    // Cross-Border Trade & Customs Invoices (Frontier 3)
    public DbSet<CountryTradeConfig> CountryTradeConfigs => Set<CountryTradeConfig>();
    public DbSet<FxExchangeRate> FxExchangeRates => Set<FxExchangeRate>();
    public DbSet<CustomsCommercialInvoice> CustomsCommercialInvoices => Set<CustomsCommercialInvoice>();

    // Predictive Demand Forecasting & Supplier Restock (Frontier 4)
    public DbSet<ProductSalesVelocity> ProductSalesVelocities => Set<ProductSalesVelocity>();
    public DbSet<StockoutProjectionAlert> StockoutProjectionAlerts => Set<StockoutProjectionAlert>();
    public DbSet<SupplierScorecard> SupplierScorecards => Set<SupplierScorecard>();
    public DbSet<AutomatedRestockOrder> AutomatedRestockOrders => Set<AutomatedRestockOrder>();

    // RBAC & Security Audit Trail (Frontier 5)
    public DbSet<SystemRole> SystemRoles => Set<SystemRole>();
    public DbSet<RolePermissionClaim> RolePermissionClaims => Set<RolePermissionClaim>();
    public DbSet<SecurityAuditLogEntry> SecurityAuditLogEntries => Set<SecurityAuditLogEntry>();

    // Technical SEO & SERP Rank Tracking Hub (Domain 11)
    public DbSet<SeoAuditReport> SeoAuditReports => Set<SeoAuditReport>();
    public DbSet<PageAuditResult> PageAuditResults => Set<PageAuditResult>();
    public DbSet<SerpKeywordTracking> SerpKeywordTrackings => Set<SerpKeywordTracking>();
    public DbSet<AeoBrandMention> AeoBrandMentions => Set<AeoBrandMention>();

    // AI Sales Funnel Builder & 1-Click Upsells (Domain 15)
    public DbSet<SalesFunnel> SalesFunnels => Set<SalesFunnel>();
    public DbSet<FunnelStep> FunnelSteps => Set<FunnelStep>();
    public DbSet<OrderBumpOffer> OrderBumpOffers => Set<OrderBumpOffer>();
    public DbSet<OneClickUpsellOffer> OneClickUpsellOffers => Set<OneClickUpsellOffer>();
    public DbSet<FunnelConversionEvent> FunnelConversionEvents => Set<FunnelConversionEvent>();

    // Closed-Loop Delivered ROAS & Autonomous Ad Spend Optimizer (Next-Gen Frontier 1)
    public DbSet<CampaignAdSpendRecord> CampaignAdSpendRecords => Set<CampaignAdSpendRecord>();
    public DbSet<AdSetDeliveredRoasMetric> AdSetDeliveredRoasMetrics => Set<AdSetDeliveredRoasMetric>();
    public DbSet<AdOptimizationRule> AdOptimizationRules => Set<AdOptimizationRule>();
    public DbSet<AdPlatformExecutionLog> AdPlatformExecutionLogs => Set<AdPlatformExecutionLog>();

    // In-House Fleet Dispatcher & Mobile Courier Driver Delivery Engine (Next-Gen Frontier 2)
    public DbSet<DeliveryDriver> DeliveryDrivers => Set<DeliveryDriver>();
    public DbSet<DriverRunSheet> DriverRunSheets => Set<DriverRunSheet>();
    public DbSet<RunSheetStop> RunSheetStops => Set<RunSheetStop>();
    public DbSet<ProofOfDeliveryRecord> ProofOfDeliveryRecords => Set<ProofOfDeliveryRecord>();
    public DbSet<DriverCashReconciliation> DriverCashReconciliations => Set<DriverCashReconciliation>();

    // Visual WhatsApp Interactive Flow Engine & State Machine (Next-Gen Frontier 3)
    public DbSet<FlowDefinition> FlowDefinitions => Set<FlowDefinition>();
    public DbSet<FlowExecutionSession> FlowExecutionSessions => Set<FlowExecutionSession>();

    // Intelligent Dynamic Pricing & Aging Inventory Liquidation Matrix (Next-Gen Frontier 4)
    public DbSet<StockAgingLedger> StockAgingLedgers => Set<StockAgingLedger>();
    public DbSet<DynamicMarkdownPolicy> DynamicMarkdownPolicies => Set<DynamicMarkdownPolicy>();
    public DbSet<LiquidationBundleOffer> LiquidationBundleOffers => Set<LiquidationBundleOffer>();

    // Autonomous Appointment Scheduling & Multi-Host Calendar Engine (Category 16)
    public DbSet<BookingEventType> BookingEventTypes => Set<BookingEventType>();
    public DbSet<HostAvailabilitySchedule> HostAvailabilitySchedules => Set<HostAvailabilitySchedule>();
    public DbSet<CalendarBookingRecord> CalendarBookingRecords => Set<CalendarBookingRecord>();

    // In-House Email Deliverability Guardian & Mailbox Warmup (Category 01)
    public DbSet<DomainDnsHealthRecord> DomainDnsHealthRecords => Set<DomainDnsHealthRecord>();
    public DbSet<EmailSpamAnalysisReport> EmailSpamAnalysisReports => Set<EmailSpamAnalysisReport>();
    public DbSet<MailboxWarmupCadence> MailboxWarmupCadences => Set<MailboxWarmupCadence>();
    public DbSet<WarmupExchangeLog> WarmupExchangeLogs => Set<WarmupExchangeLog>();

    // Real-Time Product Telemetry & Sovereign Analytics (Category 14)
    public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();
    public DbSet<VisitorSession> VisitorSessions => Set<VisitorSession>();

    // SaaS Subscription Billing & Quota Enforcement (Section 11)
    public DbSet<BillingInvoiceRecord> BillingInvoiceRecords => Set<BillingInvoiceRecord>();

    // Horizon 1: Mobile Courier Companion & Headless COD Checkout
    public DbSet<DriverMobileSession> DriverMobileSessions => Set<DriverMobileSession>();
    public DbSet<DoorstepProofOfDelivery> DoorstepProofOfDeliveries => Set<DoorstepProofOfDelivery>();
    public DbSet<CodCheckoutSession> CodCheckoutSessions => Set<CodCheckoutSession>();
    public DbSet<WilayaZone> WilayaZones => Set<WilayaZone>();

    // Horizon 3: Autonomous UGC Creative Engine & Competitor Intelligence Radar
    public DbSet<UgcVideoScript> UgcVideoScripts => Set<UgcVideoScript>();
    public DbSet<CompetitorAdRecord> CompetitorAdRecords => Set<CompetitorAdRecord>();
    public DbSet<CompetitorPriceTracker> CompetitorPriceTrackers => Set<CompetitorPriceTracker>();

    // Horizon 4: Enterprise B2B Financials & Developer Gateway
    public DbSet<B2BCreditAccount> B2BCreditAccounts => Set<B2BCreditAccount>();
    public DbSet<FactoringReceivableRecord> FactoringReceivableRecords => Set<FactoringReceivableRecord>();
    public DbSet<TenantApiKey> TenantApiKeys => Set<TenantApiKey>();
    public DbSet<WebhookSubscriptionRecord> WebhookSubscriptionRecords => Set<WebhookSubscriptionRecord>();
    public DbSet<WebhookDeliveryAttemptLog> WebhookDeliveryAttemptLogs => Set<WebhookDeliveryAttemptLog>();

    // Digital Goods, Expiring Download Tokens & PIM Variant Matrix (Frontier 2)
    public DbSet<DigitalAsset> DigitalAssets => Set<DigitalAsset>();
    public DbSet<DigitalDownloadToken> DigitalDownloadTokens => Set<DigitalDownloadToken>();
    public DbSet<ProductLicenseKey> ProductLicenseKeys => Set<ProductLicenseKey>();
    public DbSet<PimVariantMatrix> PimVariantMatrices => Set<PimVariantMatrix>();

    // Dynamic Form Builder & Cryptographic PDF Seal Registry (Frontier 3)
    public DbSet<DynamicFormDefinition> DynamicFormDefinitions => Set<DynamicFormDefinition>();
    public DbSet<DynamicFormSubmission> DynamicFormSubmissions => Set<DynamicFormSubmission>();
    public DbSet<CryptographicPdfRecord> CryptographicPdfRecords => Set<CryptographicPdfRecord>();

    // Document Tracking & Multi-Signer Envelope Engine (Category 12: documenso & papermark)
    public DbSet<DocumentEnvelope> DocumentEnvelopes => Set<DocumentEnvelope>();
    public DbSet<EnvelopeSigner> EnvelopeSigners => Set<EnvelopeSigner>();
    public DbSet<DocumentDwellTelemetry> DocumentDwellTelemetries => Set<DocumentDwellTelemetry>();
    public DbSet<DocumentAccessGate> DocumentAccessGates => Set<DocumentAccessGate>();

    // Headless Publication CMS & MEGA Newsletter Engine (Category 13: Ghost & Beehiiv)
    public DbSet<PublicationArticle> PublicationArticles => Set<PublicationArticle>();
    public DbSet<NewsletterBroadcast> NewsletterBroadcasts => Set<NewsletterBroadcast>();

    // Enterprise Sales Forecasting, Quota Attainment & Commissions (PRD Section 4.13)
    public DbSet<SalesForecastPeriod> SalesForecastPeriods => Set<SalesForecastPeriod>();
    public DbSet<SalesForecastRepQuota> SalesForecastRepQuotas => Set<SalesForecastRepQuota>();
    public DbSet<SalesCommissionPlan> SalesCommissionPlans => Set<SalesCommissionPlan>();
    public DbSet<SalesCommissionStatement> SalesCommissionStatements => Set<SalesCommissionStatement>();

    // In-App Feedback & Public Feature Voting Board (Category 10: fider, formbricks, Canny)
    public DbSet<FeatureFeedbackPost> FeatureFeedbackPosts => Set<FeatureFeedbackPost>();
    public DbSet<FeatureFeedbackVote> FeatureFeedbackVotes => Set<FeatureFeedbackVote>();

    // Phase 5: Safe AI Agent Framework & Tool Sandbox (PRD Phase 5)
    public DbSet<AiAgentBudget> AiAgentBudgets => Set<AiAgentBudget>();
    public DbSet<AiToolExecutionLog> AiToolExecutionLogs => Set<AiToolExecutionLog>();
    public DbSet<AgentMission> AgentMissions => Set<AgentMission>();
    public DbSet<AgentMissionStep> AgentMissionSteps => Set<AgentMissionStep>();

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
