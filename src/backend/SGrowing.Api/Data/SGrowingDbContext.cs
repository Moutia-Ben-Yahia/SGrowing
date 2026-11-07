using SGrowing.Core.Entities;
using SGrowing.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace SGrowing.Api.Data;

public class SGrowingDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public Guid? CurrentTenantId => (_tenantContext != null && _tenantContext.HasTenant) ? _tenantContext.TenantId : null;
    public string? CurrentTenantIdString => CurrentTenantId?.ToString();
    public Guid? CurrentWorkspaceId => _tenantContext?.WorkspaceId;

    public SGrowingDbContext(DbContextOptions<SGrowingDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<OutboxMessageEntity> TransactionalOutboxMessages => Set<OutboxMessageEntity>();
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

    // Customer Journeys (Phase 3 & Horizon 15 DAG Engine)
    public DbSet<CustomerEvent> CustomerEvents => Set<CustomerEvent>();
    public DbSet<CustomerJourney> CustomerJourneys => Set<CustomerJourney>();
    public DbSet<JourneyStep> JourneySteps => Set<JourneyStep>();
    public DbSet<JourneyEnrollment> JourneyEnrollments => Set<JourneyEnrollment>();
    public DbSet<JourneyDefinition> JourneyDefinitions => Set<JourneyDefinition>();
    public DbSet<JourneyInstance> JourneyInstances => Set<JourneyInstance>();

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
    public DbSet<ShipmentRecord> ShipmentRecords => Set<ShipmentRecord>();
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
    public DbSet<InChatCartSession> InChatCartSessions => Set<InChatCartSession>();
    public DbSet<ExternalStoreSyncRecord> ExternalStoreSyncRecords => Set<ExternalStoreSyncRecord>();
    public DbSet<GoogleSheetsSyncConfig> GoogleSheetsSyncConfigs => Set<GoogleSheetsSyncConfig>();

    // Autonomous Fulfillment & Operations Expansion
    public DbSet<VoiceConfirmationCampaign> VoiceConfirmationCampaigns => Set<VoiceConfirmationCampaign>();
    public DbSet<OutboundCallAttempt> OutboundCallAttempts => Set<OutboundCallAttempt>();
    public DbSet<MetaCatalogItem> MetaCatalogItems => Set<MetaCatalogItem>();
    public DbSet<MetaCatalogSyncFeed> MetaCatalogSyncFeeds => Set<MetaCatalogSyncFeed>();
    public DbSet<StorefrontDomain> StorefrontDomains => Set<StorefrontDomain>();
    public DbSet<BrandThemeTokens> BrandThemeTokens => Set<BrandThemeTokens>();
    public DbSet<CustomerReputationScorecard> CustomerReputationScorecards => Set<CustomerReputationScorecard>();
    public DbSet<MicroDepositPaymentLink> MicroDepositPaymentLinks => Set<MicroDepositPaymentLink>();

    // Enterprise Operations: Smart Routing, Retention LTV, Multi-Hub WMS, Live Commerce & Float
    public DbSet<PostPurchaseSurvey> PostPurchaseSurveys => Set<PostPurchaseSurvey>();
    public DbSet<ProductBurnRateConfig> ProductBurnRateConfigs => Set<ProductBurnRateConfig>();
    public DbSet<ReplenishmentSchedule> ReplenishmentSchedules => Set<ReplenishmentSchedule>();
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

    // Cross-Border Trade & Customs Invoices (Frontier 3)

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
    public DbSet<DevSandboxSessionEntity> DevSandboxSessions => Set<DevSandboxSessionEntity>();
    public DbSet<DevRateLimitBucketEntity> DevRateLimitBuckets => Set<DevRateLimitBucketEntity>();
    public DbSet<UnifiedInboxThreadEntity> UnifiedInboxThreads => Set<UnifiedInboxThreadEntity>();
    public DbSet<UnifiedInboxMessageEntity> UnifiedInboxMessages => Set<UnifiedInboxMessageEntity>();
    public DbSet<B2BProposalEntity> B2BProposalRecords => Set<B2BProposalEntity>();
    public DbSet<B2BProposalLineItemEntity> B2BProposalLineItemRecords => Set<B2BProposalLineItemEntity>();
    public DbSet<B2BUsageRecordEntity> B2BUsageRecords => Set<B2BUsageRecordEntity>();
    public DbSet<SwarmMissionEntity> SwarmMissions => Set<SwarmMissionEntity>();
    public DbSet<SwarmTaskEntity> SwarmTasks => Set<SwarmTaskEntity>();

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

    // Strategic Recommendations: Ever-Teams, Open-Mercato, Ever-Demand & Ever-Gauzy
    public DbSet<ProjectRecord> Projects => Set<ProjectRecord>();
    public DbSet<TaskItemRecord> TaskItems => Set<TaskItemRecord>();
    public DbSet<TaskCommentRecord> TaskComments => Set<TaskCommentRecord>();
    public DbSet<ClientPortalTokenRecord> ClientPortalTokens => Set<ClientPortalTokenRecord>();
    public DbSet<ClientPortalFeedbackRecord> ClientPortalFeedbacks => Set<ClientPortalFeedbackRecord>();

    public DbSet<WebResearchRecord> WebResearchRecords => Set<WebResearchRecord>();
    public DbSet<CrawlerSecretRecord> CrawlerSecretRecords => Set<CrawlerSecretRecord>();

    public DbSet<WarehouseStockRecord> WarehouseStockRecords => Set<WarehouseStockRecord>();

    public DbSet<OrganizationRecord> OrganizationRecords => Set<OrganizationRecord>();
    public DbSet<BranchRecord> BranchRecords => Set<BranchRecord>();
    public DbSet<TeamRecord> TeamRecords => Set<TeamRecord>();
    public DbSet<TeamMemberRecord> TeamMemberRecords => Set<TeamMemberRecord>();
    public DbSet<DealMarginRecord> DealMarginRecords => Set<DealMarginRecord>();

    // Frontier Enhancements & Ingested Capability Expansions
    public DbSet<WhatsAppInstanceRecord> WhatsAppInstances => Set<WhatsAppInstanceRecord>();
    public DbSet<AutonomousFunnelRunRecord> AutonomousFunnelRuns => Set<AutonomousFunnelRunRecord>();
    public DbSet<FunnelSplitTestRecord> FunnelSplitTests => Set<FunnelSplitTestRecord>();
    public DbSet<AgentPersonaRecord> AgentPersonas => Set<AgentPersonaRecord>();
    public DbSet<AgentHeartbeatRecord> AgentHeartbeatRecords => Set<AgentHeartbeatRecord>();
    public DbSet<MediaVaultRecord> MediaVaultRecords => Set<MediaVaultRecord>();
    public DbSet<InvoiceRecord> Invoices => Set<InvoiceRecord>();
    public DbSet<InvoiceLineItemRecord> InvoiceLineItems => Set<InvoiceLineItemRecord>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<CurrencyRateRecord> CurrencyRates => Set<CurrencyRateRecord>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValueRecord> CustomFieldValues => Set<CustomFieldValueRecord>();
    public DbSet<WorkflowDefinitionRecord> WorkflowDefinitionRecords => Set<WorkflowDefinitionRecord>();
    public DbSet<CartReservationLockRecord> CartReservationLocks => Set<CartReservationLockRecord>();
    public DbSet<WhatsAppNewsletterChannelRecord> WhatsAppNewsletterChannels => Set<WhatsAppNewsletterChannelRecord>();
    public DbSet<WhatsAppNewsletterPostRecord> WhatsAppNewsletterPosts => Set<WhatsAppNewsletterPostRecord>();
    public DbSet<WhatsAppPairingSessionRecord> WhatsAppPairingSessions => Set<WhatsAppPairingSessionRecord>();
    public DbSet<WhatsAppInteractiveOrderConfirmationRecord> WhatsAppInteractiveOrderConfirmations => Set<WhatsAppInteractiveOrderConfirmationRecord>();

    // Enterprise Wiki Strategic Pillars (Waves 1-4)
    public DbSet<CallCenterOrderLockRecord> CallCenterOrderLocks => Set<CallCenterOrderLockRecord>();
    public DbSet<CodNetMarginLedgerEntry> CodNetMarginLedgerEntries => Set<CodNetMarginLedgerEntry>();
    public DbSet<ConversationPresenceSession> ConversationPresenceSessions => Set<ConversationPresenceSession>();
    public DbSet<CsatSurveyResult> CsatSurveyResults => Set<CsatSurveyResult>();
    public DbSet<TenantSnapshotRecord> TenantSnapshotRecords => Set<TenantSnapshotRecord>();
    public DbSet<TenantRebillingConfig> TenantRebillingConfigs => Set<TenantRebillingConfig>();
    public DbSet<WaterfallEnrichmentCascade> WaterfallEnrichmentCascades => Set<WaterfallEnrichmentCascade>();
    public DbSet<EnrichmentExecutionLog> EnrichmentExecutionLogs => Set<EnrichmentExecutionLog>();
    public DbSet<SeoMultipageGraphReport> SeoMultipageGraphReports => Set<SeoMultipageGraphReport>();
    public DbSet<SerpRankHistoryRecord> SerpRankHistoryRecords => Set<SerpRankHistoryRecord>();
    public DbSet<InAppMicroSurveyRecord> InAppMicroSurveyRecords => Set<InAppMicroSurveyRecord>();
    public DbSet<MicroSurveyResponsePartial> MicroSurveyResponsePartials => Set<MicroSurveyResponsePartial>();
    public DbSet<AndroidSmsGatewayDevice> AndroidSmsGatewayDevices => Set<AndroidSmsGatewayDevice>();
    public DbSet<AndroidSmsDispatchRecord> AndroidSmsDispatchRecords => Set<AndroidSmsDispatchRecord>();
    public DbSet<VoiceCallAmdRecord> VoiceCallAmdRecords => Set<VoiceCallAmdRecord>();
    public DbSet<VoicemailDropTemplate> VoicemailDropTemplates => Set<VoicemailDropTemplate>();
    public DbSet<CommunityFeedbackPost> CommunityFeedbackPosts => Set<CommunityFeedbackPost>();
    public DbSet<FeedbackPostVote> FeedbackPostVotes => Set<FeedbackPostVote>();

    // Horizon 2: Schema Partitioning & Persistence (Domains B3, C6, E1)
    public DbSet<CustomerIdentityEntity> CustomerIdentities => Set<CustomerIdentityEntity>();
    public DbSet<CustomerProfileEntity> CustomerProfiles => Set<CustomerProfileEntity>();
    public DbSet<IdentityLinkEntity> IdentityLinks => Set<IdentityLinkEntity>();
    public DbSet<VisitorSessionEntity> VisitorSessionEntities => Set<VisitorSessionEntity>();
    public DbSet<VisitorSessionEntity> CustomerVisitorSessions => Set<VisitorSessionEntity>();
    public DbSet<CapiConversionEntity> CapiConversions => Set<CapiConversionEntity>();
    public DbSet<AudienceSyncEntity> AudienceSyncs => Set<AudienceSyncEntity>();
    public DbSet<CampaignRoasSnapshotEntity> CampaignRoasSnapshots => Set<CampaignRoasSnapshotEntity>();
    public DbSet<WorkflowDagEntity> WorkflowDags => Set<WorkflowDagEntity>();
    public DbSet<WorkflowTelemetryTraceEntity> WorkflowTelemetryTraces => Set<WorkflowTelemetryTraceEntity>();
    public DbSet<CashflowForecastEntity> CashflowForecasts => Set<CashflowForecastEntity>();
    public DbSet<TopicalClusterEntity> TopicClusters => Set<TopicalClusterEntity>();
    public DbSet<DynamicElasticityPricingEntity> PricingElasticityModels => Set<DynamicElasticityPricingEntity>();
    public DbSet<RetentionBanditArmEntity> RetentionBanditArms => Set<RetentionBanditArmEntity>();
    public DbSet<ApprovalRequestEntity> ApprovalRequests => Set<ApprovalRequestEntity>();
    public DbSet<CourierDeliveryRecordEntity> CourierDeliveryRecords => Set<CourierDeliveryRecordEntity>();
    public DbSet<DocumentSealRecordEntity> DocumentSealRecords => Set<DocumentSealRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ====================================================================
        // PRD Section 5.1 & 5.13: Explicit PostgreSQL Schemas & Composite Indexes
        // ====================================================================

        // --------------------------------------------------------------------
        // Schema: platform
        // --------------------------------------------------------------------
        modelBuilder.Entity<OutboxMessageEntity>(b =>
        {
            b.ToTable("outbox_messages", "platform");
            b.HasKey(o => o.Id);
            b.Property(o => o.EventType).IsRequired().HasMaxLength(200);
            b.Property(o => o.PayloadJson).IsRequired();
            b.HasIndex(o => new { o.Status, o.NextAttemptAtUtc });
            b.HasIndex(o => new { o.TenantId, o.CreatedAtUtc });
            b.HasIndex(o => o.CorrelationId);
        });
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages", "platform");
            b.HasKey(o => o.Id);
            b.Property(o => o.EventType).IsRequired().HasMaxLength(100);
            b.HasIndex(o => new { o.ProcessedOnUtc, o.OccurredOnUtc });
            b.HasIndex(o => new { o.TenantId, o.OccurredOnUtc });
        });

        // --------------------------------------------------------------------
        // Schema: identity
        // --------------------------------------------------------------------
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users", "identity");
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).IsRequired().HasMaxLength(256);
            b.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        });

        modelBuilder.Entity<SystemRole>(b =>
        {
            b.ToTable("SystemRoles", "identity");
            b.HasKey(r => r.Id);
            b.Property(r => r.RoleName).IsRequired().HasMaxLength(100);
            b.HasIndex(r => new { r.TenantId, r.RoleName });
        });

        modelBuilder.Entity<RolePermissionClaim>(b =>
        {
            b.ToTable("RolePermissionClaims", "identity");
            b.HasKey(p => p.Id);
            b.Property(p => p.Resource).IsRequired().HasMaxLength(100);
            b.Property(p => p.Action).IsRequired().HasMaxLength(100);
            b.HasIndex(p => new { p.TenantId, p.RoleId });
        });

        modelBuilder.Entity<TenantApiKey>(b =>
        {
            b.ToTable("TenantApiKeys", "identity");
            b.HasKey(k => k.Id);
            b.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(50);
            b.Property(k => k.KeyMasked).IsRequired().HasMaxLength(100);
            b.Property(k => k.HashedSecret).IsRequired();
            b.HasIndex(k => new { k.TenantId, k.KeyPrefix });
        });

        // --------------------------------------------------------------------
        // Schema: tenancy
        // --------------------------------------------------------------------
        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants", "tenancy");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            b.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<Workspace>(b =>
        {
            b.ToTable("Workspaces", "tenancy");
            b.HasKey(w => w.Id);
            b.Property(w => w.Name).IsRequired().HasMaxLength(200);
            b.Property(w => w.Slug).IsRequired().HasMaxLength(100);
            b.HasIndex(w => new { w.TenantId, w.Slug }).IsUnique();
        });

        modelBuilder.Entity<WilayaZone>(b =>
        {
            b.ToTable("WilayaZones", "tenancy");
            b.HasKey(z => z.Id);
            b.Property(z => z.Code).IsRequired().HasMaxLength(50);
            b.Property(z => z.CountryCode).IsRequired().HasMaxLength(10);
            b.Property(z => z.StandardShippingFee).HasPrecision(18, 2);
            b.HasIndex(z => new { z.CountryCode, z.Code });
        });

        // --------------------------------------------------------------------
        // Schema: crm
        // --------------------------------------------------------------------
        modelBuilder.Entity<Contact>(b =>
        {
            b.ToTable("Contacts", "crm");
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
            b.ToTable("Companies", "crm");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(c => new { c.TenantId, c.Domain });
        });

        modelBuilder.Entity<Deal>(b =>
        {
            b.ToTable("Deals", "crm");
            b.HasKey(d => d.Id);
            b.Property(d => d.Title).IsRequired().HasMaxLength(200);
            b.Property(d => d.Amount).HasPrecision(18, 2);
            b.HasIndex(d => new { d.TenantId, d.Stage });
            b.HasIndex(d => new { d.TenantId, d.CreatedAtUtc });
            b.HasIndex(d => new { d.TenantId, d.ContactId });
        });

        modelBuilder.Entity<Activity>(b =>
        {
            b.ToTable("Activities", "crm");
            b.HasKey(a => a.Id);
            b.HasIndex(a => new { a.TenantId, a.CreatedAtUtc });
            b.HasIndex(a => new { a.TenantId, a.ContactId, a.CreatedAtUtc });
        });

        modelBuilder.Entity<CustomFieldDefinition>(b =>
        {
            b.ToTable("CustomFieldDefinitions", "crm");
            b.HasKey(f => f.Id);
            b.Property(f => f.EntityType).IsRequired().HasMaxLength(50);
            b.Property(f => f.FieldName).IsRequired().HasMaxLength(100);
            b.HasIndex(f => new { f.TenantId, f.EntityType, f.FieldName });
        });

        modelBuilder.Entity<CustomFieldValueRecord>(b =>
        {
            b.ToTable("CustomFieldValues", "crm");
            b.HasKey(v => v.Id);
            b.Property(v => v.ValueNumber).HasPrecision(18, 4);
            b.HasIndex(v => new { v.TenantId, v.EntityId, v.FieldDefinitionId });
        });

        // --------------------------------------------------------------------
        // Schema: sales
        // --------------------------------------------------------------------
        modelBuilder.Entity<QuoteProposal>(b =>
        {
            b.ToTable("QuoteProposals", "sales");
            b.HasKey(q => q.Id);
            b.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(100);
            b.Property(q => q.Subtotal).HasPrecision(18, 2);
            b.Property(q => q.DiscountPercent).HasPrecision(18, 2);
            b.Property(q => q.TaxPercent).HasPrecision(18, 2);
            b.Property(q => q.TotalAmount).HasPrecision(18, 2);
            b.HasIndex(q => new { q.TenantId, q.QuoteNumber });
            b.HasIndex(q => new { q.TenantId, q.ContactId });
        });

        modelBuilder.Entity<QuoteLineItem>(b =>
        {
            b.ToTable("QuoteLineItems", "sales");
            b.HasKey(i => i.Id);
            b.Property(i => i.UnitPrice).HasPrecision(18, 2);
            b.HasIndex(i => i.QuoteProposalId);
        });

        modelBuilder.Entity<DigitalContract>(b =>
        {
            b.ToTable("DigitalContracts", "sales");
            b.HasKey(c => c.Id);
            b.Property(c => c.Title).IsRequired().HasMaxLength(200);
            b.Property(c => c.ContractValue).HasPrecision(18, 2);
            b.HasIndex(c => new { c.TenantId, c.Status });
            b.HasIndex(c => new { c.TenantId, c.Title });
        });

        modelBuilder.Entity<SalesCommissionPlan>(b =>
        {
            b.ToTable("SalesCommissionPlans", "sales");
            b.HasKey(p => p.Id);
            b.Property(p => p.PlanName).IsRequired().HasMaxLength(200);
            b.Property(p => p.BaseRatePercent).HasPrecision(18, 2);
            b.Property(p => p.QuotaThresholdPercent).HasPrecision(18, 2);
            b.Property(p => p.AcceleratedRatePercent).HasPrecision(18, 2);
            b.HasIndex(p => new { p.TenantId, p.PlanName });
        });

        modelBuilder.Entity<SalesCommissionStatement>(b =>
        {
            b.ToTable("SalesCommissionStatements", "sales");
            b.HasKey(s => s.Id);
            b.Property(s => s.TotalDealsValue).HasPrecision(18, 2);
            b.Property(s => s.CalculatedCommission).HasPrecision(18, 2);
            b.HasIndex(s => new { s.TenantId, s.PeriodId, s.SalesRepUserId });
            b.HasIndex(s => new { s.TenantId, s.Status });
        });

        // --------------------------------------------------------------------
        // Schema: commerce
        // --------------------------------------------------------------------
        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products", "commerce");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).IsRequired().HasMaxLength(200);
            b.Property(p => p.Slug).IsRequired().HasMaxLength(100);
            b.Property(p => p.BasePrice).HasPrecision(18, 2);
            b.HasIndex(p => new { p.TenantId, p.Slug });
        });

        modelBuilder.Entity<ProductVariant>(b =>
        {
            b.ToTable("ProductVariants", "commerce");
            b.HasKey(pv => pv.Id);
            b.Property(pv => pv.SKU).IsRequired().HasMaxLength(100);
            b.Property(pv => pv.Price).HasPrecision(18, 2);
            b.HasIndex(pv => new { pv.TenantId, pv.ProductId, pv.SKU });
        });

        modelBuilder.Entity<CodOrder>(b =>
        {
            b.ToTable("CodOrders", "commerce");
            b.HasKey(o => o.Id);
            b.Property(o => o.OrderNumber).IsRequired().HasMaxLength(100);
            b.Property(o => o.TotalAmount).HasPrecision(18, 2);
            b.Property(o => o.ShippingFee).HasPrecision(18, 2);
            b.HasIndex(o => new { o.TenantId, o.OrderNumber });
            b.HasIndex(o => new { o.TenantId, o.Status });
            b.HasIndex(o => new { o.TenantId, o.CreatedAtUtc });
        });

        modelBuilder.Entity<Warehouse>(b =>
        {
            b.ToTable("Warehouses", "commerce");
            b.HasKey(w => w.Id);
            b.Property(w => w.Name).IsRequired().HasMaxLength(200);
            b.Property(w => w.Code).IsRequired().HasMaxLength(50);
            b.HasIndex(w => new { w.TenantId, w.Code });
        });

        modelBuilder.Entity<WarehouseInventory>(b =>
        {
            b.ToTable("WarehouseInventories", "commerce");
            b.HasKey(wi => wi.Id);
            b.Property(wi => wi.Sku).IsRequired().HasMaxLength(100);
            b.HasIndex(wi => new { wi.TenantId, wi.WarehouseId, wi.Sku });
        });

        modelBuilder.Entity<CartReservationLockRecord>(b =>
        {
            b.ToTable("CartReservationLocks", "commerce");
            b.HasKey(l => l.Id);
            b.Property(l => l.Sku).IsRequired().HasMaxLength(100);
            b.Property(l => l.Status).IsRequired().HasMaxLength(50);
            b.HasIndex(l => new { l.TenantId, l.Status, l.ExpiresAtUtc });
            b.HasIndex(l => new { l.TenantId, l.WarehouseId, l.Sku });
        });

        // --------------------------------------------------------------------
        // Schema: engagement
        // --------------------------------------------------------------------
        modelBuilder.Entity<Inbox>(b =>
        {
            b.ToTable("Inboxes", "engagement");
            b.HasKey(i => i.Id);
            b.Property(i => i.Name).IsRequired().HasMaxLength(100);
            b.HasIndex(i => new { i.TenantId, i.ChannelType });
        });

        modelBuilder.Entity<Conversation>(b =>
        {
            b.ToTable("Conversations", "engagement");
            b.HasKey(c => c.Id);
            b.HasIndex(c => new { c.TenantId, c.Status });
            b.HasIndex(c => new { c.TenantId, c.Status, c.LastMessageAtUtc });
            b.HasIndex(c => new { c.TenantId, c.ContactId });
        });

        modelBuilder.Entity<Message>(b =>
        {
            b.ToTable("Messages", "engagement");
            b.HasKey(m => m.Id);
            b.HasIndex(m => new { m.TenantId, m.CreatedAtUtc });
            b.HasIndex(m => new { m.TenantId, m.ConversationId, m.CreatedAtUtc });
            b.HasIndex(m => new { m.TenantId, m.ExternalMessageId });
        });

        modelBuilder.Entity<AiDraft>(b =>
        {
            b.ToTable("AiDrafts", "engagement");
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.TenantId, d.ConversationId, d.Status });
        });

        modelBuilder.Entity<SenderAccount>(b =>
        {
            b.ToTable("SenderAccounts", "engagement");
            b.HasKey(s => s.Id);
            b.Property(s => s.Email).IsRequired().HasMaxLength(256);
            b.HasIndex(s => new { s.TenantId, s.Email });
            b.HasIndex(s => new { s.TenantId, s.Status });
        });

        modelBuilder.Entity<WhatsAppTemplate>(b =>
        {
            b.ToTable("WhatsAppTemplates", "engagement");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(t => new { t.TenantId, t.Name });
        });

        modelBuilder.Entity<BroadcastCampaign>(b =>
        {
            b.ToTable("BroadcastCampaigns", "engagement");
            b.HasKey(bc => bc.Id);
            b.Property(bc => bc.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(bc => new { bc.TenantId, bc.Status });
        });

        // --------------------------------------------------------------------
        // Schema: automation
        // --------------------------------------------------------------------
        modelBuilder.Entity<CustomerJourney>(b =>
        {
            b.ToTable("CustomerJourneys", "automation");
            b.HasKey(j => j.Id);
            b.Property(j => j.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(j => new { j.TenantId, j.Status });
        });

        modelBuilder.Entity<JourneyStep>(b =>
        {
            b.ToTable("JourneySteps", "automation");
            b.HasKey(s => s.Id);
            b.HasIndex(s => new { s.TenantId, s.JourneyId });
        });

        modelBuilder.Entity<WorkflowGraph>(b =>
        {
            b.ToTable("WorkflowGraphs", "automation");
            b.HasKey(g => g.Id);
            b.Property(g => g.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(g => new { g.TenantId, g.IsActive });
        });

        // --------------------------------------------------------------------
        // Schema: support
        // --------------------------------------------------------------------
        modelBuilder.Entity<HelpCategory>(b =>
        {
            b.ToTable("HelpCategories", "support");
            b.HasKey(h => h.Id);
            b.Property(h => h.Name).IsRequired().HasMaxLength(200);
            b.Property(h => h.Slug).IsRequired().HasMaxLength(100);
            b.HasIndex(h => new { h.TenantId, h.Slug });
        });

        modelBuilder.Entity<HelpArticle>(b =>
        {
            b.ToTable("HelpArticles", "support");
            b.HasKey(a => a.Id);
            b.Property(a => a.Title).IsRequired().HasMaxLength(256);
            b.Property(a => a.Slug).IsRequired().HasMaxLength(150);
            b.HasIndex(a => new { a.TenantId, a.CategoryId, a.Slug });
        });

        modelBuilder.Entity<SlaPolicy>(b =>
        {
            b.ToTable("SlaPolicies", "support");
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(s => new { s.TenantId, s.Priority });
        });

        modelBuilder.Entity<SurveyForm>(b =>
        {
            b.ToTable("SurveyForms", "support");
            b.HasKey(f => f.Id);
            b.Property(f => f.Title).IsRequired().HasMaxLength(200);
            b.HasIndex(f => new { f.TenantId, f.IsActive });
        });

        modelBuilder.Entity<SurveyResponse>(b =>
        {
            b.ToTable("SurveyResponses", "support");
            b.HasKey(r => r.Id);
            b.HasIndex(r => new { r.TenantId, r.SurveyFormId });
            b.HasIndex(r => new { r.TenantId, r.ContactId });
        });

        // --------------------------------------------------------------------
        // Schema: billing
        // --------------------------------------------------------------------
        modelBuilder.Entity<TenantSubscription>(b =>
        {
            b.ToTable("TenantSubscriptions", "billing");
            b.HasKey(s => s.Id);
            b.Property(s => s.MonthlyPrice).HasPrecision(18, 2);
            b.HasIndex(s => new { s.TenantId, s.Status });
        });

        modelBuilder.Entity<FeatureEntitlement>(b =>
        {
            b.ToTable("FeatureEntitlements", "billing");
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.TenantId, e.PlanTier });
        });

        modelBuilder.Entity<UsageMeterRecord>(b =>
        {
            b.ToTable("UsageMeterRecords", "billing");
            b.HasKey(u => u.Id);
            b.HasIndex(u => new { u.TenantId, u.Metric, u.RecordedAtUtc });
        });

        modelBuilder.Entity<BillingInvoiceRecord>(b =>
        {
            b.ToTable("BillingInvoiceRecords", "billing");
            b.HasKey(i => i.Id);
            b.Property(i => i.AmountDue).HasPrecision(18, 2);
            b.HasIndex(i => new { i.TenantId, i.Status });
        });

        // --------------------------------------------------------------------
        // Schema: analytics
        // --------------------------------------------------------------------
        modelBuilder.Entity<AttributionTouchpoint>(b =>
        {
            b.ToTable("AttributionTouchpoints", "analytics");
            b.HasKey(a => a.Id);
            b.Property(a => a.Cost).HasPrecision(18, 2);
            b.HasIndex(a => new { a.TenantId, a.ContactId, a.OccurredAtUtc });
        });

        modelBuilder.Entity<TelemetryEvent>(b =>
        {
            b.ToTable("TelemetryEvents", "analytics");
            b.HasKey(t => t.Id);
            b.HasIndex(t => new { t.TenantId, t.TimestampUtc });
        });

        modelBuilder.Entity<VisitorSession>(b =>
        {
            b.ToTable("VisitorSessions", "analytics");
            b.HasKey(v => v.Id);
            b.HasIndex(v => new { v.TenantId, v.SessionId });
            b.HasIndex(v => new { v.TenantId, v.VisitorId });
        });

        // --------------------------------------------------------------------
        // Schema: ai
        // --------------------------------------------------------------------
        modelBuilder.Entity<DynamicAgentTool>(b =>
        {
            b.ToTable("DynamicAgentTools", "ai");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired().HasMaxLength(150);
            b.HasIndex(t => new { t.TenantId, t.Name });
        });

        modelBuilder.Entity<AiAgentBudget>(b =>
        {
            b.ToTable("AiAgentBudgets", "ai");
            b.HasKey(b => b.Id);
            b.Property(b => b.MaxMonthlyCostUsd).HasPrecision(18, 2);
            b.Property(b => b.CostSpentUsd).HasPrecision(18, 2);
            b.HasIndex(b => new { b.TenantId, b.CurrentMonthStartUtc });
        });

        modelBuilder.Entity<AgentMission>(b =>
        {
            b.ToTable("AgentMissions", "ai");
            b.HasKey(m => m.Id);
            b.Property(m => m.TotalBudgetUsd).HasPrecision(18, 2);
            b.Property(m => m.SpentBudgetUsd).HasPrecision(18, 2);
            b.HasIndex(m => new { m.TenantId, m.Status });
        });

        modelBuilder.Entity<DocumentChunk>(b =>
        {
            b.ToTable("DocumentChunks", "ai");
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.TenantId, d.DocumentId });
            b.HasIndex(d => new { d.TenantId, d.DocumentTitle });
        });

        modelBuilder.Entity<CashflowForecastEntity>(b =>
        {
            b.ToTable("CashflowForecasts", "analytics");
            b.HasKey(c => c.Id);
            b.Property(c => c.ProjectedCashBalance).HasPrecision(18, 2);
            b.Property(c => c.NetBurnRate).HasPrecision(18, 2);
            b.Property(c => c.ConfidenceIntervalLower).HasPrecision(18, 2);
            b.Property(c => c.ConfidenceIntervalUpper).HasPrecision(18, 2);
            b.HasIndex(c => new { c.TenantId, c.ForecastDateUtc });
        });

        modelBuilder.Entity<TopicalClusterEntity>(b =>
        {
            b.ToTable("TopicClusters", "engagement");
            b.HasKey(t => t.Id);
            b.HasIndex(t => new { t.TenantId, t.TargetKeyword });
            b.HasIndex(t => new { t.TenantId, t.ClusterId });
        });

        modelBuilder.Entity<DynamicElasticityPricingEntity>(b =>
        {
            b.ToTable("PricingElasticityModels", "sales");
            b.HasKey(p => p.Id);
            b.Property(p => p.BasePrice).HasPrecision(18, 2);
            b.Property(p => p.OptimalPrice).HasPrecision(18, 2);
            b.Property(p => p.MaxContributionMargin).HasPrecision(18, 2);
            b.Property(p => p.CurrentElasticityScore).HasPrecision(18, 4);
            b.HasIndex(p => new { p.TenantId, p.ProductId });
        });

        modelBuilder.Entity<RetentionBanditArmEntity>(b =>
        {
            b.ToTable("RetentionBanditArms", "engagement");
            b.HasKey(r => r.Id);
            b.Property(r => r.TotalLtvRetainedDzd).HasPrecision(18, 2);
            b.HasIndex(r => new { r.TenantId, r.IncentiveType });
        });

        modelBuilder.Entity<ApprovalRequestEntity>(b =>
        {
            b.ToTable("ApprovalRequests", "ai");
            b.HasKey(a => a.Id);
            b.Property(a => a.FinancialImpactUsd).HasPrecision(18, 2);
            b.HasIndex(a => new { a.TenantId, a.Status });
            b.HasIndex(a => a.ApprovalTokenHash);
        });

        modelBuilder.Entity<CourierDeliveryRecordEntity>(b =>
        {
            b.ToTable("CourierDeliveryRecords", "commerce");
            b.HasKey(c => c.Id);
            b.Property(c => c.CashCollected).HasPrecision(18, 2);
            b.HasIndex(c => new { c.TenantId, c.ParcelId });
            b.HasIndex(c => new { c.TenantId, c.CourierDriverId });
        });

        modelBuilder.Entity<DocumentSealRecordEntity>(b =>
        {
            b.ToTable("DocumentSealRecords", "compliance");
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.TenantId, d.DocumentId });
            b.HasIndex(d => d.VerificationToken);
        });

        // --------------------------------------------------------------------
        // Critical Composite Indexes (PRD Section 5.13)
        // --------------------------------------------------------------------
        modelBuilder.Entity<CustomerEvent>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => new { e.TenantId, e.OccurredAtUtc });
            b.HasIndex(e => new { e.TenantId, e.ContactId, e.OccurredAtUtc });
        });

        modelBuilder.Entity<WorkflowExecution>(b =>
        {
            b.ToTable("WorkflowExecutions", "automation");
            b.HasKey(w => w.Id);
            b.HasIndex(w => new { w.TenantId, w.Status });
            b.HasIndex(w => new { w.TenantId, w.CreatedAtUtc });
        });

        modelBuilder.Entity<OutboundCampaign>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasIndex(c => new { c.TenantId, c.Status });
            b.HasIndex(c => new { c.TenantId, c.CreatedAtUtc });
        });

        // Strategic Recommendations Configurations (Ever-Teams, Open-Mercato, Ever-Demand & Ever-Gauzy)
        modelBuilder.Entity<ProjectRecord>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => new { p.TenantId, p.DealId });
            b.HasIndex(p => new { p.TenantId, p.ContractId });
        });

        modelBuilder.Entity<TaskItemRecord>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasIndex(t => new { t.TenantId, t.ProjectId, t.Status });
        });

        modelBuilder.Entity<TaskCommentRecord>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasIndex(c => new { c.TenantId, c.TaskId });
        });

        modelBuilder.Entity<ClientPortalTokenRecord>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasIndex(t => new { t.TenantId, t.ProjectId });
            b.HasIndex(t => t.Token).IsUnique();
        });

        modelBuilder.Entity<ClientPortalFeedbackRecord>(b =>
        {
            b.HasKey(f => f.Id);
            b.HasIndex(f => new { f.TenantId, f.ProjectId });
            b.HasIndex(f => new { f.TenantId, f.TaskId });
        });

        modelBuilder.Entity<WebResearchRecord>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasIndex(r => new { r.TenantId, r.Domain });
        });

        modelBuilder.Entity<CrawlerSecretRecord>(b =>
        {
            b.HasKey(cs => cs.Id);
            b.HasIndex(cs => new { cs.TenantId, cs.Provider });
        });

        modelBuilder.Entity<WarehouseStockRecord>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasIndex(s => new { s.TenantId, s.WarehouseId, s.Sku });
            b.Property(s => s.UnitCost).HasPrecision(18, 2);
            b.Property(s => s.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<OrganizationRecord>(b =>
        {
            b.HasKey(o => o.Id);
            b.HasIndex(o => new { o.TenantId, o.Code });
        });

        modelBuilder.Entity<BranchRecord>(b =>
        {
            b.HasKey(br => br.Id);
            b.HasIndex(br => new { br.TenantId, br.OrganizationId });
        });

        modelBuilder.Entity<TeamRecord>(b =>
        {
            b.HasKey(tm => tm.Id);
            b.HasIndex(tm => new { tm.TenantId, tm.BranchId });
        });

        modelBuilder.Entity<TeamMemberRecord>(b =>
        {
            b.HasKey(m => m.Id);
            b.HasIndex(m => new { m.TenantId, m.TeamId });
            b.Property(m => m.HourlyLaborRate).HasPrecision(18, 2);
        });

        modelBuilder.Entity<DealMarginRecord>(b =>
        {
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.TenantId, d.DealId });
            b.HasIndex(d => new { d.TenantId, d.OrganizationId });
            b.Property(d => d.Revenue).HasPrecision(18, 2);
            b.Property(d => d.CostOfGoodsSold).HasPrecision(18, 2);
            b.Property(d => d.CourierShippingFees).HasPrecision(18, 2);
            b.Property(d => d.OperatorLaborHours).HasPrecision(18, 2);
            b.Property(d => d.OperatorHourlyRate).HasPrecision(18, 2);
            b.Property(d => d.OperatorLaborCost).HasPrecision(18, 2);
            b.Property(d => d.TrueGrossMargin).HasPrecision(18, 2);
            b.Property(d => d.GrossMarginPercentage).HasPrecision(18, 2);
        });


        modelBuilder.Entity<WhatsAppInstanceRecord>(b =>
        {
            b.HasKey(w => w.Id);
            b.HasIndex(w => new { w.TenantId, w.InstanceName }).IsUnique();
        });

        modelBuilder.Entity<WhatsAppPairingSessionRecord>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasIndex(s => new { s.TenantId, s.PairingCode });
            b.HasIndex(s => new { s.TenantId, s.InstanceName });
        });

        modelBuilder.Entity<WhatsAppInteractiveOrderConfirmationRecord>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasIndex(c => new { c.TenantId, c.OrderId });
            b.HasIndex(c => new { c.TenantId, c.Status });
        });

        modelBuilder.Entity<AutonomousFunnelRunRecord>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasIndex(r => new { r.TenantId, r.StartedAtUtc });
        });

        modelBuilder.Entity<AgentPersonaRecord>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.RoleKey).IsUnique();
        });

        modelBuilder.Entity<AgentHeartbeatRecord>(b =>
        {
            b.HasKey(h => h.Id);
            b.HasIndex(h => new { h.TenantId, h.ExecutedAtUtc });
        });

        modelBuilder.Entity<MediaVaultRecord>(b =>
        {
            b.HasKey(m => m.Id);
            b.HasIndex(m => new { m.TenantId, m.StorageKey });
        });

        modelBuilder.Entity<VoiceCallSession>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.CallSid).IsRequired().HasMaxLength(100);
            b.Property(s => s.FromNumber).HasMaxLength(50);
            b.Property(s => s.ToNumber).HasMaxLength(50);
            b.HasIndex(s => new { s.TenantId, s.CallSid });
            b.HasIndex(s => new { s.TenantId, s.ContactId });
        });

        modelBuilder.Entity<CadenceTouchpoint>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.BodyTemplate).IsRequired();
            b.HasIndex(t => new { t.TenantId, t.CampaignId });
        });

        modelBuilder.Entity<TouchpointExecution>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Status).IsRequired().HasMaxLength(50);
            b.HasIndex(e => new { e.TenantId, e.Status, e.ScheduledAtUtc });
        });

        // ====================================================================
        // Horizon 2: Schema Partitioning & Persistence (Domains B3, C6, E1)
        // ====================================================================

        // Schema customer_data (Domain B3: Customer Data / CDP)
        modelBuilder.Entity<CustomerIdentityEntity>(b =>
        {
            b.ToTable("identities", "customer_data");
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.Type).IsRequired().HasMaxLength(50);
            b.Property(e => e.Value).IsRequired().HasMaxLength(256);
            b.Property(e => e.CanonicalProfileId).IsRequired().HasMaxLength(100);
            b.HasIndex(e => new { e.TenantId, e.Type, e.Value }).IsUnique();
            b.HasIndex(e => new { e.TenantId, e.CanonicalProfileId });
        });

        modelBuilder.Entity<CustomerProfileEntity>(b =>
        {
            b.ToTable("profiles", "customer_data");
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.CanonicalProfileId).IsRequired().HasMaxLength(100);
            b.Property(e => e.UnifiedTraitsJson).HasColumnType("jsonb");
            b.HasIndex(e => new { e.TenantId, e.CanonicalProfileId });
        });

        modelBuilder.Entity<IdentityLinkEntity>(b =>
        {
            b.ToTable("identity_links", "customer_data");
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.SourceIdentifierJson).HasColumnType("jsonb");
            b.Property(e => e.TargetIdentifierJson).HasColumnType("jsonb");
            b.HasIndex(e => e.TenantId);
        });

        modelBuilder.Entity<VisitorSessionEntity>(b =>
        {
            b.ToTable("visitor_sessions", "customer_data");
            b.HasKey(e => e.SessionId);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.AnonymousId).IsRequired().HasMaxLength(100);
            b.Property(e => e.PageViewsJson).HasColumnType("jsonb");
            b.Property(e => e.UtmParametersJson).HasColumnType("jsonb");
            b.HasIndex(e => new { e.TenantId, e.AnonymousId });
        });

        // Schema advertising (Domain C6: Advertising & CAPI Sync)
        modelBuilder.Entity<CapiConversionEntity>(b =>
        {
            b.ToTable("conversions", "advertising");
            b.HasKey(e => e.EventId);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            b.Property(e => e.EventName).IsRequired().HasMaxLength(100);
            b.Property(e => e.Currency).HasMaxLength(10);
            b.Property(e => e.ConversionValue).HasPrecision(18, 4);
            b.HasIndex(e => new { e.TenantId, e.Platform, e.EventId }).IsUnique();
        });

        modelBuilder.Entity<AudienceSyncEntity>(b =>
        {
            b.ToTable("audience_syncs", "advertising");
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.AudienceId).IsRequired().HasMaxLength(100);
            b.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            b.HasIndex(e => new { e.TenantId, e.AudienceId });
            b.HasIndex(e => new { e.TenantId, e.Platform });
        });

        modelBuilder.Entity<CampaignRoasSnapshotEntity>(b =>
        {
            b.ToTable("campaign_roas_snapshots", "advertising");
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.CampaignId).IsRequired().HasMaxLength(100);
            b.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            b.Property(e => e.Spend).HasPrecision(18, 4);
            b.Property(e => e.Revenue).HasPrecision(18, 4);
            b.Property(e => e.CurrentRoas).HasPrecision(18, 4);
            b.Property(e => e.RecommendedSpend).HasPrecision(18, 4);
            b.HasIndex(e => new { e.TenantId, e.CampaignId });
            b.HasIndex(e => new { e.TenantId, e.Platform });
        });

        // Schema automation (Domain E1: Workflow DAG & Telemetry)
        modelBuilder.Entity<WorkflowDagEntity>(b =>
        {
            b.ToTable("workflows", "automation");
            b.HasKey(e => e.WorkflowId);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.Name).IsRequired().HasMaxLength(200);
            b.Property(e => e.NodesJson).HasColumnType("jsonb");
            b.Property(e => e.EdgesJson).HasColumnType("jsonb");
            b.HasIndex(e => new { e.TenantId, e.WorkflowId }).IsUnique();
        });

        modelBuilder.Entity<WorkflowTelemetryTraceEntity>(b =>
        {
            b.ToTable("telemetry_traces", "automation");
            b.HasKey(e => e.ExecutionId);
            b.Property(e => e.TenantId).IsRequired();
            b.Property(e => e.WorkflowId).IsRequired().HasMaxLength(100);
            b.Property(e => e.ContactId).HasMaxLength(100);
            b.Property(e => e.Status).IsRequired().HasMaxLength(50);
            b.Property(e => e.StepsJson).HasColumnType("jsonb");
            b.HasIndex(e => new { e.TenantId, e.WorkflowId });
            b.HasIndex(e => new { e.TenantId, e.Status });
        });

        modelBuilder.Entity<DevSandboxSessionEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            b.HasIndex(e => e.TenantId).IsUnique();
        });

        modelBuilder.Entity<DevRateLimitBucketEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.BucketKey).IsRequired().HasMaxLength(200);
            b.HasIndex(e => e.BucketKey).IsUnique();
        });

        modelBuilder.Entity<UnifiedInboxThreadEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            b.HasIndex(e => new { e.TenantId, e.Status });
            b.HasIndex(e => new { e.TenantId, e.CustomerEmail });
            b.HasIndex(e => new { e.TenantId, e.CustomerPhone });
            b.HasMany(e => e.Messages).WithOne(m => m.Thread).HasForeignKey(m => m.ThreadId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UnifiedInboxMessageEntity>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.TenantId).IsRequired().HasMaxLength(100);
            b.HasIndex(m => new { m.ThreadId, m.TimestampUtc });
        });

        modelBuilder.Entity<B2BProposalEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            b.HasIndex(e => new { e.TenantId, e.Status });
            b.HasMany(e => e.LineItems).WithOne(i => i.Proposal).HasForeignKey(i => i.ProposalId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<B2BProposalLineItemEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.ProposalId);
        });

        modelBuilder.Entity<B2BUsageRecordEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            b.HasIndex(e => new { e.TenantId, e.MetricType, e.TimestampUtc });
        });

        modelBuilder.Entity<SwarmMissionEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            b.Property(e => e.MissionId).IsRequired().HasMaxLength(100);
            b.HasIndex(e => new { e.TenantId, e.MissionId }).IsUnique();
            b.HasMany(e => e.Tasks).WithOne(t => t.Mission).HasForeignKey(t => t.MissionEntityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SwarmTaskEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TaskId).IsRequired().HasMaxLength(100);
            b.HasIndex(e => new { e.MissionEntityId, e.TaskId });
        });

        ConfigureGlobalTenantFilters(modelBuilder);
    }

    private static readonly System.Reflection.MethodInfo SetGuidFilterMethod =
        typeof(SGrowingDbContext).GetMethod(nameof(SetGuidTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

    private static readonly System.Reflection.MethodInfo SetStringFilterMethod =
        typeof(SGrowingDbContext).GetMethod(nameof(SetStringTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

    private void ConfigureGlobalTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType != null || entityType.IsOwned())
                continue;

            var clrType = entityType.ClrType;
            if (clrType == null)
                continue;

            var tenantIdProperty = entityType.FindProperty("TenantId");
            if (tenantIdProperty != null)
            {
                if (tenantIdProperty.ClrType == typeof(Guid))
                {
                    SetGuidFilterMethod.MakeGenericMethod(clrType).Invoke(this, new object[] { modelBuilder });
                }
                else if (tenantIdProperty.ClrType == typeof(string))
                {
                    SetStringFilterMethod.MakeGenericMethod(clrType).Invoke(this, new object[] { modelBuilder });
                }
            }
        }
    }

    private void SetGuidTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !CurrentTenantId.HasValue || EF.Property<Guid>(e, "TenantId") == CurrentTenantId.GetValueOrDefault());
    }

    private void SetStringTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => CurrentTenantIdString == null || EF.Property<string>(e, "TenantId") == CurrentTenantIdString);
    }

    public bool BypassTenantIsolationValidation { get; set; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantIsolation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateTenantIsolation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateTenantIsolation()
    {
        if (BypassTenantIsolationValidation) return;
        if (!CurrentTenantId.HasValue) return;

        var currentTenantId = CurrentTenantId.Value;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var prop = entry.Metadata.FindProperty("TenantId");
            if (prop != null)
            {
                var val = entry.Property("TenantId").CurrentValue;
                if (val is Guid tenantId && tenantId != Guid.Empty && tenantId != currentTenantId)
                {
                    throw new UnauthorizedAccessException($"Cross-tenant write operation rejected. Entity '{entry.Entity.GetType().Name}' belongs to tenant '{tenantId}', but context is scoped to tenant '{currentTenantId}'.");
                }
                else if (val is string strTenantId && !string.IsNullOrWhiteSpace(strTenantId) && !string.Equals(strTenantId, CurrentTenantIdString, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"Cross-tenant write operation rejected. Entity '{entry.Entity.GetType().Name}' belongs to tenant '{strTenantId}', but context is scoped to tenant '{CurrentTenantIdString}'.");
                }
            }
        }
    }
}
