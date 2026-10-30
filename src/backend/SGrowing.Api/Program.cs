using System.Text.Json;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Documents;
using SGrowing.Api.Modules.Gateways;
using SGrowing.Api.Modules.Analytics;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Api.Modules.KnowledgeBase.Autopilot;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.Automation;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Security;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Api.Modules.Voice;
using SGrowing.Api.Modules.Finance;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Api.Modules.Dev;
using SGrowing.Api.Modules.SEO;
using SGrowing.Api.Modules.Funnels;
using SGrowing.Api.Modules.Calendar;
using SGrowing.Api.Modules.Deliverability;
using SGrowing.Api.Modules.Telemetry;
using SGrowing.Api.Modules.Agents;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Api.Modules.Billing;
using SGrowing.Api.Modules.Outbound.Spintax;
using SGrowing.Api.Modules.Outbound.EmailVerification;
using SGrowing.Api.Modules.Outbound.Enrichment;
using SGrowing.Api.Modules.Conversations.Mime;
using SGrowing.Api.Modules.Conversations.Macros;
using SGrowing.Api.Modules.Conversations.Sla;
using SGrowing.Api.Modules.CRM.Import;
using SGrowing.Api.Modules.CRM.ContractSigning;
using SGrowing.Api.Modules.CRM.CustomFields;
using SGrowing.Api.Modules.Commerce.Shipping;
using SGrowing.Api.Modules.Commerce.AntiFraud;
using SGrowing.Api.Modules.Commerce.Connectors;
using SGrowing.Api.Modules.Projects;
using SGrowing.Api.Modules.Outbound.Research;
using SGrowing.Api.Modules.Commerce.Warehouses;
using SGrowing.Api.Modules.Organizations;
using SGrowing.Api.Modules.Workforce.Timesheets;
using SGrowing.Api.Modules.Commerce.CartReservations;
using SGrowing.Api.Modules.Commerce.CallCenter;
using SGrowing.Api.Modules.Conversations.PresenceAndCsat;
using SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;
using SGrowing.Api.Modules.Feedback.MicroSurveys;
using SGrowing.Api.Modules.Gateways.Android;
using SGrowing.Api.Modules.Feedback.Community;
using SGrowing.Api.Modules.Funnels.Autonomous;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Api.Modules.Storage.Media;
using SGrowing.Api.Modules.Sales;
using SGrowing.Api.Modules.Billing.Invoicing;
using SGrowing.Api.Modules.Agents.Git;
using SGrowing.Api.Common;
using Scalar.AspNetCore;
using SGrowing.Core.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SGrowing.Api.Services;
using SGrowing.Core.Events;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Channels;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;
using SGrowing.Core.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// 1. Tenancy & Database
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddDbContext<SGrowingDbContext>((sp, options) =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connStr))
    {
        options.UseNpgsql(connStr);
    }
    else
    {
        options.UseInMemoryDatabase("SGrowingDev");
    }
});

// 2. Core & Outbox Services
builder.Services.AddScoped<IOutbox, OutboxService>();
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddScoped<IAiCopilotService, AiCopilotService>();
builder.Services.AddScoped<ChannelIngressService>();

// 3. Outbound AI SDR Services
builder.Services.AddScoped<MailboxService>();
builder.Services.AddSingleton<IAiSdrService, AiSdrService>();
builder.Services.AddSingleton<ReplySentimentClassifier>();
builder.Services.AddScoped<ProspectReplyHandoffService>();
builder.Services.AddScoped<CadenceDispatcherService>();
builder.Services.AddScoped<CreateCampaignHandler>();
builder.Services.AddScoped<EnrollProspectsHandler>();
builder.Services.AddScoped<IPowerDialerService, PowerDialerService>();

// 4. Journeys, Feedback & Knowledge Base
builder.Services.AddScoped<JourneyExecutionService>();
builder.Services.AddScoped<IStatefulJourneyEngineService, StatefulJourneyEngineService>();
builder.Services.AddScoped<FeedbackService>();
builder.Services.AddScoped<HelpCenterRagService>();

// 4. Channel Adapters (Messaging, Social & SMS)
builder.Services.AddSingleton<WhatsAppCloudAdapter>();
builder.Services.AddSingleton<EvolutionApiAdapter>();
builder.Services.AddSingleton<EmailAdapter>();
builder.Services.AddSingleton<TelegramAdapter>();
builder.Services.AddSingleton<DiscordAdapter>();
builder.Services.AddSingleton<MetaGraphAdapter>();
builder.Services.AddSingleton<XTwitterAdapter>();
builder.Services.AddSingleton<SmsAdapter>();

// 5. Inbound Slices Handlers
builder.Services.AddScoped<CreateContactHandler>();
builder.Services.AddScoped<GetContactHandler>();
builder.Services.AddScoped<SearchContactsHandler>();
builder.Services.AddScoped<SendMessageHandler>();
builder.Services.AddScoped<ChangeStatusHandler>();
builder.Services.AddScoped<GetConversationMessagesHandler>();
builder.Services.AddScoped<ApproveAiDraftHandler>();

// 6. Autonomous AI Agent, SLA, Identity, Broadcasts & Assisted Commerce
builder.Services.AddScoped<IAgentTool, KnowledgeBaseTool>();
builder.Services.AddScoped<IAgentTool, OrderLookupTool>();
builder.Services.AddScoped<IAgentTool, EscalateToHumanTool>();
builder.Services.AddScoped<AutonomousAiAgentService>();
builder.Services.AddScoped<IdentityResolverService>();
builder.Services.AddScoped<BroadcastDispatcherService>();
builder.Services.AddScoped<AppointmentBookingService>();
builder.Services.AddScoped<CheckoutLinkService>();
builder.Services.AddScoped<SlaMonitoringService>();

// 7. Voice & Telephony
builder.Services.AddScoped<VoiceTelephonyService>();
builder.Services.AddScoped<RealtimeVoiceAgentService>();
builder.Services.AddScoped<VoiceAiStreamService>();

// 8. Social Publishing & Deflection
builder.Services.AddSingleton<ISocialPostRepository, InMemorySocialPostRepository>();
builder.Services.AddScoped<SocialPublishingService>();
builder.Services.AddScoped<SocialCommentDeflectionService>();
builder.Services.AddSingleton<ISocialCommentToDmService, SocialCommentToDmService>();

// 9. Multi-Touch AI SDR
builder.Services.AddScoped<LinkedInOutreachAdapter>();
builder.Services.AddScoped<MultiTouchCadenceOrchestrator>();

// 10. B2B CRM Accounts & Visual Workflows
builder.Services.AddScoped<CompanyAccountService>();
builder.Services.AddScoped<WorkflowEngineService>();

// 11. Enterprise Horizons: Voice WebSocket Streaming, OAuth Vault, pgvector RAG, Contracts & Enrichment
builder.Services.AddSingleton<BargeInInterruptionService>();
builder.Services.AddSingleton<StreamingVoiceWebSocketHandler>();
builder.Services.AddSingleton<AesEncryptionService>();
builder.Services.AddScoped<ITenantSecretsVaultService, TenantSecretsVaultService>();
builder.Services.AddSingleton<DocumentChunkingService>();
builder.Services.AddScoped<VectorRagRetrievalService>();
builder.Services.AddScoped<LeadEnrichmentService>();
builder.Services.AddScoped<ContractSigningService>();

// 12. Commercial Supremacy: PIM Catalog, CPQ, Storefront Mesh, Realtime Voice Bridge, Privacy
builder.Services.AddScoped<ProductCatalogService>();
builder.Services.AddScoped<InChatCpqService>();
builder.Services.AddScoped<StripePaymentWebhookHandler>();
builder.Services.AddScoped<ShopifyWebhookReceiver>();
builder.Services.AddScoped<AbandonedCartRecoveryService>();
builder.Services.AddSingleton<OpenAiRealtimeBridgeService>();
builder.Services.AddSingleton<BrowserSoftphoneSignalingService>();
builder.Services.AddScoped<CustomerPrivacyService>();

// 13. Global Strategic Leadership: Revenue Attribution, SaaS Metering, Cron DLQ, AI Gateway
builder.Services.AddScoped<MultiTouchAttributionService>();
builder.Services.AddScoped<TenantUsageMeteringService>();
builder.Services.AddScoped<QuietHoursGuardService>();
builder.Services.AddSingleton<PromptFirewallService>();
builder.Services.AddSingleton<AiModelRouterService>();
builder.Services.AddScoped<DynamicAgentToolRegistry>();

// 14. Omnichannel Commerce, Logistics, Defensive WMS, Identity & Cadences
builder.Services.AddScoped<ICodConfirmationService, CodConfirmationService>();
builder.Services.AddScoped<ICourierLogisticsHubService, CourierLogisticsHubService>();
builder.Services.AddScoped<IDefensiveWmsService, DefensiveWmsService>();
builder.Services.AddScoped<ISalesCadenceEngine, SalesCadenceEngine>();
builder.Services.AddScoped<ICustomerIdentityGraphService, CustomerIdentityGraphService>();
builder.Services.AddScoped<ICustomer360AttributionService, Customer360AttributionService>();
builder.Services.AddScoped<ICustomerSelfServicePortalService, CustomerSelfServicePortalService>();

// 15. Server-Side Ad Tracking, Courier Clearing, In-Chat Commerce & Storefront Mesh
builder.Services.AddScoped<IServerConversionMeshService, ServerConversionMeshService>();
builder.Services.AddScoped<ICourierRemittanceClearingService, CourierRemittanceClearingService>();
builder.Services.AddScoped<IConversationalCartEngine, ConversationalCartEngine>();
builder.Services.AddScoped<IExternalStoreAndSheetsService, ExternalStoreAndSheetsService>();

// 16. Autonomous Operations: Voice Dialer, Barcode Fulfillment, WhatsApp Catalog, White-Labeling & Reputation
builder.Services.AddScoped<IAutomatedVoiceDialerService, AutomatedVoiceDialerService>();
builder.Services.AddScoped<IBarcodeFulfillmentService, BarcodeFulfillmentService>();
builder.Services.AddScoped<IWhatsAppCatalogEngine, WhatsAppCatalogEngine>();
builder.Services.AddScoped<IStorefrontDomainService, StorefrontDomainService>();
builder.Services.AddScoped<ICustomerReputationService, CustomerReputationService>();

// 17. Enterprise Operations: Smart Routing, Retention LTV, Multi-Hub WMS, Live Commerce & Float
builder.Services.AddScoped<ISmartCourierRoutingService, SmartCourierRoutingService>();
builder.Services.AddScoped<IPostPurchaseRetentionService, PostPurchaseRetentionService>();
builder.Services.AddScoped<IMultiWarehouseService, MultiWarehouseService>();
builder.Services.AddScoped<ILiveCommerceOverlayService, LiveCommerceOverlayService>();
builder.Services.AddScoped<ITreasuryFloatService, TreasuryFloatService>();

// 18. Enterprise Scale: Creator Affiliate Escrow, Reverse Exchanges, Cross-Border Trade, Demand Forecasting & RBAC Security
builder.Services.AddScoped<ICreatorAffiliateService, CreatorAffiliateService>();
builder.Services.AddScoped<IReverseExchangeService, ReverseExchangeService>();
builder.Services.AddScoped<ICrossBorderTradeService, CrossBorderTradeService>();
builder.Services.AddScoped<IDemandForecastingService, DemandForecastingService>();
builder.Services.AddScoped<ISecurityAuditAndRbacService, SecurityAuditAndRbacService>();

// 19. Technical SEO, SERP Rank Tracking & AI Funnels
builder.Services.AddScoped<ISeoAuditService, SeoAuditService>();
builder.Services.AddScoped<ISalesFunnelService, SalesFunnelService>();

// 20. Next-Gen Frontiers: Delivered ROAS Optimizer, Fleet Dispatcher, WhatsApp Flow Engine, Dynamic Liquidation
builder.Services.AddScoped<IAdSpendOptimizerService, AdSpendOptimizerService>();
builder.Services.AddScoped<IInHouseFleetService, InHouseFleetService>();
builder.Services.AddScoped<IWhatsAppFlowEngineService, WhatsAppFlowEngineService>();
builder.Services.AddScoped<IInventoryLiquidationService, InventoryLiquidationService>();

// 21. Strategic Horizons: Calendar Scheduling, Deliverability Guardian, Telemetry & SaaS Billing
builder.Services.AddScoped<ICalendarSchedulingService, CalendarSchedulingService>();
builder.Services.AddScoped<IEmailDeliverabilityService, EmailDeliverabilityService>();
builder.Services.AddScoped<IProductTelemetryService, ProductTelemetryService>();
builder.Services.AddScoped<IDatabasePartitioningService, DatabasePartitioningService>();
builder.Services.AddScoped<IClickstreamBufferService, ClickstreamBufferService>();
builder.Services.AddScoped<ISaasBillingService, SaasBillingService>();
builder.Services.AddScoped<IPlanQuotaEnforcementService, PlanQuotaEnforcementService>();

// 22. Frontiers 2-4: Digital Goods & PIM Matrix, Dynamic Forms & Developer Webhooks
builder.Services.AddScoped<IDigitalGoodsService, DigitalGoodsService>();
builder.Services.AddScoped<IDynamicFormService, DynamicFormService>();
builder.Services.AddScoped<IDeveloperWebhookService, DeveloperWebhookService>();

// 23. Strategic Horizons 1-4: Courier Companion, COD Checkout, UGC Creative Generator, Competitor Radar, B2B Credit & Developer Gateway
builder.Services.AddSingleton<IEventStreamBroker, InMemoryEventStreamBroker>();
builder.Services.AddScoped<ICourierCompanionService, CourierCompanionService>();
builder.Services.AddScoped<ICodCheckoutService, CodCheckoutService>();
builder.Services.AddScoped<IUgcCreativeGeneratorService, UgcCreativeGeneratorService>();
builder.Services.AddScoped<ICompetitorIntelligenceService, CompetitorIntelligenceService>();
builder.Services.AddScoped<IB2BCreditAndFactoringService, B2BCreditAndFactoringService>();
builder.Services.AddScoped<IDeveloperGatewayService, DeveloperGatewayService>();

// 24. Strategic Competitor Convergence: Document Tracking, Headless CMS, Sales Forecasting & Public Roadmap
builder.Services.AddScoped<IDocumentTrackingService, DocumentTrackingService>();
builder.Services.AddScoped<IPublicationCmsService, PublicationCmsService>();
builder.Services.AddScoped<ISalesForecastingService, SalesForecastingService>();
builder.Services.AddScoped<IFeatureFeedbackService, FeatureFeedbackService>();

// 25. Safe AI Agent Framework, Permissioned Tool Execution & Conversational BI (PRD Phase 5)
builder.Services.AddScoped<IAiAgentOrchestratorService, AiAgentOrchestratorService>();
builder.Services.AddScoped<IAutonomousAgentMissionService, AutonomousAgentMissionService>();
builder.Services.AddScoped<IConversationalBiService, ConversationalBiService>();

// 26. Track 1-3 Enhancements: PDF Generator, Gateways & Semantic RAG
builder.Services.AddSingleton<IDocumentPdfGeneratorService, DocumentPdfGeneratorService>();
builder.Services.AddSingleton<IWhatsAppGatewayClient, EvolutionApiGatewayClient>();
builder.Services.AddHttpClient<IWhatsAppCloudApiClient, WhatsAppCloudApiAdapter>();
builder.Services.AddSingleton<ILiveKitMediaClient, LiveKitMediaServerAdapter>();
builder.Services.AddSingleton<ISmtpSocketClient, SmtpSocketClientAdapter>();
builder.Services.AddSingleton<ICourierLogisticsGateway, CourierLogisticsGateway>();
builder.Services.AddScoped<ISemanticVectorRagService, SemanticVectorRagService>();

// 27. Missing Core Inspiration Repository Services (Phases 1-4)
builder.Services.AddScoped<IChatbotFlowEngineService, ChatbotFlowEngineService>();
builder.Services.AddScoped<IInAppEngagementService, InAppEngagementService>();
builder.Services.AddScoped<IInAppTourService, InAppTourService>();
builder.Services.AddScoped<ICollisionDetectionAndSlaService, CollisionDetectionAndSlaService>();
builder.Services.AddScoped<IOmnichannelUnifiedDeskService, OmnichannelUnifiedDeskService>();
builder.Services.AddScoped<IWarmupPeerNetworkService, WarmupPeerNetworkService>();
builder.Services.AddScoped<IAutonomousSdrFleetService, AutonomousSdrFleetService>();
builder.Services.AddScoped<ISocialPublishingEngineService, SocialPublishingEngineService>();
builder.Services.AddScoped<IMcpServerRegistryService, McpServerRegistryService>();
builder.Services.AddScoped<IFrappeCrmEnhancementsService, FrappeCrmEnhancementsService>();
builder.Services.AddScoped<IHelpdeskCommercialContextService, HelpdeskCommercialContextService>();
builder.Services.AddScoped<ICpqQuoteAndEavService, CpqQuoteAndEavService>();
builder.Services.AddScoped<IDocumentTelemetryService, DocumentTelemetryService>();
builder.Services.AddScoped<ICryptographicPdfSealingService, CryptographicPdfSealingService>();
builder.Services.AddScoped<ISegmentTelemetryAndJourneysService, SegmentTelemetryAndJourneysService>();

// 28. Revenue Pillars: Cold Outreach & AI SDR, Deliverability, Omnichannel, CRM Data, Bot Canvas, CPQ & E-Sign, COD Anti-Fraud
builder.Services.AddSingleton<ISpintaxService, SpintaxService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IContactEnrichmentPipeline, ContactEnrichmentPipeline>();
builder.Services.AddSingleton<IWarmupThreadGeneratorService, WarmupThreadGeneratorService>();
builder.Services.AddScoped<IPlacementTestService, PlacementTestService>();
builder.Services.AddSingleton<IMimeEmailParserService, MimeEmailParserService>();
builder.Services.AddSingleton<ICannedMacroService, CannedMacroService>();
builder.Services.AddScoped<ISlaEscalationEngineService, SlaEscalationEngineService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();
builder.Services.AddSingleton<IBotSimulatorService, BotSimulatorService>();
builder.Services.AddSingleton<IMultiPartySigningService, MultiPartySigningService>();
builder.Services.AddSingleton<ICarrierShippingGateway, CarrierShippingGateway>();
builder.Services.AddScoped<ICodAntiFraudEngine, CodAntiFraudEngine>();
builder.Services.AddScoped<IShopifyConnectorService, ShopifyConnectorService>();
builder.Services.AddScoped<IWooCommerceConnectorService, WooCommerceConnectorService>();

// 29. Strategic Recommendations (Ever-Teams, Open-Mercato, Ever-Demand & Ever-Gauzy)
builder.Services.AddScoped<IProjectTaskService, ProjectTaskService>();
builder.Services.AddScoped<IScraperVaultService, ScraperVaultService>();
builder.Services.AddScoped<IDeepWebResearchService, DeepWebResearchService>();
builder.Services.AddScoped<IWarehouseRoutingService, WarehouseRoutingService>();
builder.Services.AddScoped<IOrganizationHierarchyService, OrganizationHierarchyService>();

// Ingested Capability Expansion & Strategic Next Steps
builder.Services.AddSingleton<SGrowing.Core.Common.IConcurrencyLockService, SGrowing.Core.Common.DistributedMemoryLockService>();
builder.Services.AddSingleton<IEventDeduplicationService, EventDeduplicationService>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<IWhatsAppGatewayService, WhatsAppGatewayService>();
builder.Services.AddScoped<IAutonomousFunnelPipelineService, AutonomousFunnelPipelineService>();
builder.Services.AddScoped<IFunnelSplitTestService, FunnelSplitTestService>();
builder.Services.AddScoped<IAgentPersonaService, AgentPersonaService>();
builder.Services.AddScoped<IMediaVaultService, MediaVaultService>();
builder.Services.AddScoped<IInboundMessageRouterService, InboundMessageRouterService>();
builder.Services.AddSingleton<IAudioTranscriptionService, AudioTranscriptionService>();
builder.Services.AddScoped<IOperatorAssignmentService, OperatorAssignmentService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IGitHubAutomationService, GitHubAutomationService>();
builder.Services.AddSingleton<ITokenRevocationService, TokenRevocationService>();
builder.Services.AddScoped<ICustomFieldService, CustomFieldService>();
builder.Services.AddScoped<IWorkflowGraphExecutorService, WorkflowGraphExecutorService>();
builder.Services.AddScoped<ICartReservationService, CartReservationService>();
builder.Services.AddScoped<IMultiBrokerEventFanoutService, MultiBrokerEventFanoutService>();
builder.Services.AddScoped<IWhatsAppNewsletterService, WhatsAppNewsletterService>();
builder.Services.AddScoped<IAiModelRouterService, AiModelRouterService>();
builder.Services.AddScoped<AiModelRouterService>();

// Enterprise Wiki Strategic Pillars (Waves 1-4)
builder.Services.AddScoped<ICallCenterAntiCollisionService, CallCenterAntiCollisionService>();
builder.Services.AddScoped<ICodNetMarginLedgerService, CodNetMarginLedgerService>();
builder.Services.AddScoped<IConversationPresenceService, ConversationPresenceService>();
builder.Services.AddScoped<ICsatSurveyService, CsatSurveyService>();
builder.Services.AddScoped<ITenantSnapshotService, TenantSnapshotService>();
builder.Services.AddScoped<ITenantRebillingService, TenantRebillingService>();
builder.Services.AddScoped<IWaterfallEnrichmentService, WaterfallEnrichmentService>();
builder.Services.AddScoped<ISeoMultipageGraphService, SeoMultipageGraphService>();
builder.Services.AddScoped<ISerpRankTrackerService, SerpRankTrackerService>();
builder.Services.AddScoped<IInAppMicroSurveyService, InAppMicroSurveyService>();
builder.Services.AddScoped<IAndroidSmsGatewayService, AndroidSmsGatewayService>();
builder.Services.AddScoped<IVoiceCallAmdService, VoiceCallAmdService>();
builder.Services.AddScoped<ICommunityFeedbackService, CommunityFeedbackService>();

// Frontiers 5-9: Proposal Portal, WebRTC Video Meetings, Multi-Inbox Rotation, COD Carrier Auto-Dispatch & PostgreSql RLS
builder.Services.AddScoped<IProposalPortalService, ProposalPortalService>();
builder.Services.AddScoped<IVideoMeetingRoomService, VideoMeetingRoomService>();
builder.Services.AddScoped<IMultiInboxRotationService, MultiInboxRotationService>();
builder.Services.AddScoped<ICodCarrierAutoDispatchService, CodCarrierAutoDispatchService>();
builder.Services.AddScoped<IPostgreSqlRlsService, PostgreSqlRlsService>();
builder.Services.AddSingleton<TenantRlsConnectionInterceptor>();

// Horizons 10-13: WhatsApp Pairing Code, Autonomous 15-Step Funnel, 38-Role Agent Fleet, Technical SEO Crawler
builder.Services.AddWhatsAppPairingServices();
builder.Services.AddScoped<IAutonomousFunnelBuilderService, AutonomousFunnelBuilderService>();
builder.Services.AddAgentFleetOrchestration();
builder.Services.AddScoped<ITechnicalSeoCrawlerService, TechnicalSeoCrawlerService>();
builder.Services.AddHttpClient<TechnicalSeoCrawlerService>();

// Horizon 16: Autonomous P2P Mailbox Warmup Network & ESP Spam Rescue Daemon
builder.Services.AddScoped<IP2PWarmupRescueService, P2PWarmupRescueService>();

// Horizon 18: Configure, Price, Quote (CPQ) & Margin-Guarded Quotation Engine
builder.Services.AddScoped<ICpqQuotationService, CpqQuotationService>();

// Horizon 21: B2B Wholesale Portal, Corporate Sub-Accounts & Credit Limit Ledger
builder.Services.AddScoped<IB2BWholesalePortalService, B2BWholesalePortalService>();

// Horizon 22: Self-Healing Outbound Webhook Mesh & Circuit Breaker Engine
builder.Services.AddWebhookMesh();

// Horizon 23: AI Predictive Sales Cadence & Optimal Contact Time Intelligence
builder.Services.AddScoped<IPredictiveCadenceService, PredictiveCadenceService>();

// Horizon 24: Autonomous Inventory Replenishment & Vendor Purchase Order Engine
builder.Services.AddSingleton<IInventoryReplenishmentService, InventoryReplenishmentService>();

// Horizon 25: Multi-Tenant Whitelabel Branding & Custom Domain SSL Provisioning Engine
builder.Services.AddSingleton<IWhitelabelBrandingService, WhitelabelBrandingService>();

// Horizon 26: Autonomous Cash Flow Forecasting & Working Capital Float Engine
builder.Services.AddScoped<ICashflowForecastingService, CashflowForecastingService>();

// Horizon 27: Distributed Rate Limiting & Sliding Window Token Bucket Guard
builder.Services.AddSingleton<IDistributedRateLimiterService, DistributedRateLimiterService>();

// Horizon 28: Predictive Customer Churn & Automated Retention Winback Daemon
builder.Services.AddScoped<ICustomerRetentionDaemonService, CustomerRetentionDaemonService>();

// Horizon 29: Tamper-Evident Immutable Audit Ledger with Merkle Tree Cryptographic Sealing
builder.Services.AddSingleton<IMerkleAuditLedgerService, MerkleAuditLedgerService>();

// Horizon 30: Multi-Channel Public Feedback & Feature Voting Board Engine
builder.Services.AddScoped<IFeedbackBoardService, FeedbackBoardService>();

// Horizon 31: Dynamic Multi-Currency Invoicing, Cross-Border VAT & Credit Note Ledger
builder.Services.AddScoped<ITaxAndInvoiceLedgerService, TaxAndInvoiceLedgerService>();

// Horizon 32: Hybrid Vector RAG Knowledge Base & Automated Ticket Autopilot
builder.Services.AddScoped<IHybridRagAutopilotService, HybridRagAutopilotService>();

// Horizon 33: Document Page-by-Page Reading Telemetry & Heatmap Analytics Engine
builder.Services.AddScoped<IDocumentTelemetryHeatmapService, DocumentTelemetryHeatmapService>();

builder.Services.AddHttpClient("WebScraperClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "SGrowing-Bot/2.0 (Resilient Scraper Engine)");
});

// 16. Authentication, Security & Tenant Context
var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "SGrowing_SuperSecretEnterpriseMasterKey_998877665544332211!!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SGrowing.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SGrowing.Clients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddOpenApi();

// 17. Durable Redis Stream Broker
var redisConn = builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConn))
{
    try
    {
        builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ => StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
        builder.Services.AddSingleton<IRedisStreamBroker, RedisStreamBroker>();
    }
    catch
    {
        builder.Services.AddSingleton<IRedisStreamBroker, InMemoryStreamBroker>();
    }
}
else
{
    builder.Services.AddSingleton<IRedisStreamBroker, InMemoryStreamBroker>();
}

// Daemon Heartbeat & Watchdog Service
builder.Services.AddSingleton<SGrowing.Core.Diagnostics.IDaemonHeartbeatService, SGrowing.Api.Modules.Diagnostics.DaemonHeartbeatService>();

// 18. SignalR & CORS
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<PlanQuotaEnforcementMiddleware>();

// Ensure in-memory dev db is seeded if needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SGrowingDbContext>();
    db.Database.EnsureCreated();
}

// ----------------- API Endpoints -----------------

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("SGrowing Omnichannel & AI Growth Engine API")
           .WithTheme(ScalarTheme.Moon)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});
app.MapAuthEndpoints();
app.MapDevEndpoints();
app.MapSeoEndpoints();
app.MapFunnelEndpoints();
app.MapCalendarEndpoints();
app.MapDeliverabilityEndpoints();
app.MapTelemetryEndpoints();
app.MapAnalyticsEndpoints();
app.MapBillingEndpoints();
app.MapDigitalGoodsEndpoints();
app.MapDynamicFormEndpoints();
app.MapDeveloperWebhookEndpoints();
app.MapSocialEndpoints();
app.MapPowerDialerEndpoints();
app.MapDocumentTrackingEndpoints();
app.MapPublicationCmsEndpoints();
app.MapSalesForecastingEndpoints();
app.MapFeatureFeedbackEndpoints();
app.MapAiEndpoints();
app.MapAgentMissionEndpoints();
app.MapOutboundPillarEndpoints();
app.MapDeliverabilityPillarEndpoints();
app.MapOmnichannelEndpoints();
app.MapCrmImportEndpoints();
app.MapBotCanvasEndpoints();
app.MapContractSigningEndpoints();
app.MapCommercePillarEndpoints();
app.MapCommerceConnectorEndpoints();
app.MapWhatsAppCloudWebhookEndpoints();
app.MapProjectTaskEndpoints();
app.MapDeepWebResearchEndpoints();
app.MapWarehouseRoutingEndpoints();
app.MapOrganizationHierarchyEndpoints();
app.MapTimesheetEndpoints();
app.MapWhatsAppGatewayEndpoints();
app.MapAutonomousFunnelEndpoints();
app.MapAgentPersonaEndpoints();
app.MapMediaVaultEndpoints();
app.MapInvoiceEndpoints();
app.MapPaymentWebhookEndpoints();
app.MapConversationCopilotEndpoints();
app.MapCustomFieldEndpoints();
app.MapWorkflowGraphEndpoints();
app.MapCartReservationEndpoints();
app.MapEventFanoutEndpoints();
app.MapWhatsAppNewsletterEndpoints();
app.MapAiModelRouterEndpoints();

// Enterprise Wiki Strategic Pillars Endpoints (Waves 1-4)
app.MapCallCenterAntiCollisionEndpoints();
app.MapCodNetMarginEndpoints();
app.MapConversationPresenceEndpoints();
app.MapCsatSurveyEndpoints();
app.MapTenantSnapshotEndpoints();
app.MapTenantRebillingEndpoints();
app.MapWaterfallEnrichmentEndpoints();
app.MapSeoMultipageGraphEndpoints();
app.MapSerpRankTrackerEndpoints();
app.MapInAppMicroSurveyEndpoints();
app.MapAndroidSmsGatewayEndpoints();
app.MapVoiceCallAmdEndpoints();
app.MapCommunityFeedbackEndpoints();

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", framework = ".NET 10", timestamp = DateTime.UtcNow }));
app.MapGet("/healthz/live", () => Results.Ok(new { status = "Healthy", timestampUtc = DateTime.UtcNow }));
app.MapGet("/healthz/ready", async (SGrowingDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect 
        ? Results.Ok(new { status = "Ready", database = "Connected", timestampUtc = DateTime.UtcNow }) 
        : Results.Problem("Database unavailable", statusCode: 503);
});

app.MapGet("/health/daemons", async (SGrowing.Core.Diagnostics.IDaemonHeartbeatService heartbeat) =>
{
    var report = await heartbeat.GetHealthReportAsync();
    return Results.Ok(report);
});

app.MapGet("/api/v1/health/daemons", async (SGrowing.Core.Diagnostics.IDaemonHeartbeatService heartbeat) =>
{
    var report = await heartbeat.GetHealthReportAsync();
    return Results.Ok(report);
});

// CRM Contacts
app.MapPost("/api/crm/contacts", async ([FromBody] CreateContactRequest req, CreateContactHandler handler) =>
{
    var res = await handler.HandleAsync(req);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/api/crm/contacts/{id:guid}", async (Guid id, [FromQuery] Guid tenantId, GetContactHandler handler) =>
{
    var res = await handler.HandleAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
});

app.MapGet("/api/crm/contacts", async ([FromQuery] Guid tenantId, [FromQuery] string? query, SearchContactsHandler handler) =>
{
    var res = await handler.HandleAsync(new SearchContactsQuery(tenantId, query ?? ""));
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// Conversations & Messages
app.MapGet("/api/conversations/{id:guid}/messages", async (Guid id, [FromQuery] Guid tenantId, GetConversationMessagesHandler handler) =>
{
    var res = await handler.HandleAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
});

app.MapPost("/api/conversations/{id:guid}/messages", async (Guid id, [FromBody] SendMessageRequest req, SendMessageHandler handler) =>
{
    var res = await handler.HandleAsync(req with { ConversationId = id });
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/conversations/{id:guid}/status", async (Guid id, [FromBody] ChangeStatusRequest req, ChangeStatusHandler handler) =>
{
    var res = await handler.HandleAsync(req with { ConversationId = id });
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// AI Draft Approval
app.MapPost("/api/ai/drafts/{id:guid}/approve", async (Guid id, [FromBody] ApproveAiDraftRequest req, ApproveAiDraftHandler handler) =>
{
    var res = await handler.HandleAsync(req with { DraftId = id });
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// Webhook Ingress (with Reply Sentiment Handoff hook)
app.MapPost("/api/webhooks/{channel}", async (
    string channel,
    [FromQuery] Guid tenantId,
    [FromQuery] Guid inboxId,
    HttpRequest request,
    ChannelIngressService ingressService,
    ProspectReplyHandoffService handoffService,
    WhatsAppCloudAdapter waCloud,
    EvolutionApiAdapter evoWa,
    EmailAdapter email,
    TelegramAdapter telegram,
    DiscordAdapter discord,
    MetaGraphAdapter meta,
    XTwitterAdapter xTwitter,
    SmsAdapter sms) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    InboundChannelMessage? parsed = channel.ToLower() switch
    {
        "whatsapp_cloud" => waCloud.ParseInboundWebhook(tenantId, body),
        "evolution_api" => evoWa.ParseInboundWebhook(tenantId, body),
        "email" => email.ParseInboundWebhook(tenantId, body),
        "telegram" => telegram.ParseInboundWebhook(tenantId, body),
        "discord" => discord.ParseInboundWebhook(tenantId, body),
        "meta_graph" => meta.ParseInboundWebhook(tenantId, body),
        "instagram" => meta.ParseInboundWebhook(tenantId, body),
        "facebook" => meta.ParseInboundWebhook(tenantId, body),
        "threads" => meta.ParseInboundWebhook(tenantId, body),
        "x_twitter" => xTwitter.ParseInboundWebhook(tenantId, body),
        "sms" => sms.ParseInboundWebhook(tenantId, body),
        _ => null
    };

    if (parsed == null)
        return Results.BadRequest(new { error = $"Could not parse webhook for channel: {channel}" });

    var res = await ingressService.IngestAsync(inboxId, parsed);
    if (!res.IsSuccess)
        return Results.BadRequest(new { error = res.Error });

    // Evaluate prospect reply sentiment for outbound handoff
    await handoffService.HandleReplyAsync(tenantId, res.Value.SenderId ?? Guid.Empty, res.Value.ConversationId, parsed.Content);

    return Results.Ok(new { status = "Ingested", messageId = res.Value.Id });
});

// ----------------- Outbound AI SDR Endpoints -----------------

// Mailboxes
app.MapGet("/api/outbound/mailboxes", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var mailboxes = await db.SenderAccounts.Where(s => s.TenantId == tenantId).ToListAsync();
    return Results.Ok(mailboxes);
});

app.MapPost("/api/outbound/mailboxes", async ([FromBody] SenderAccount sender, SGrowingDbContext db) =>
{
    db.SenderAccounts.Add(sender);
    await db.SaveChangesAsync();
    return Results.Ok(sender);
});

// Campaigns & Cadences
app.MapGet("/api/outbound/campaigns", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var campaigns = await db.OutboundCampaigns.Where(c => c.TenantId == tenantId).ToListAsync();
    return Results.Ok(campaigns);
});

app.MapPost("/api/outbound/campaigns", async ([FromBody] CreateCampaignRequest req, CreateCampaignHandler handler) =>
{
    var res = await handler.HandleAsync(req);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/outbound/campaigns/{id:guid}/enroll", async (Guid id, [FromBody] List<Guid> contactIds, [FromQuery] Guid tenantId, EnrollProspectsHandler handler) =>
{
    var res = await handler.HandleAsync(new EnrollProspectsRequest(tenantId, id, contactIds));
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/outbound/dispatch", async ([FromQuery] Guid tenantId, CadenceDispatcherService dispatcher) =>
{
    var count = await dispatcher.ProcessDueCadencesAsync(tenantId);
    return Results.Ok(new { processed = count });
});

// Deals / CRM Pipeline
app.MapGet("/api/crm/deals", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var deals = await db.Deals.Where(d => d.TenantId == tenantId).OrderByDescending(d => d.CreatedAtUtc).ToListAsync();
    return Results.Ok(deals);
});

// ----------------- Customer Journeys Endpoints -----------------
app.MapPost("/api/events", async ([FromQuery] Guid tenantId, [FromBody] IngestEventDto req, JourneyExecutionService service) =>
{
    var res = await service.IngestEventAsync(tenantId, req.ContactId, req.EventName, req.PayloadJson ?? "{}");
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapJourneyEndpoints();
app.MapJourneyDagEndpoints();

// ----------------- Feedback & CSAT Endpoints -----------------
app.MapGet("/api/feedback/surveys", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var forms = await db.SurveyForms.Where(f => f.TenantId == tenantId).ToListAsync();
    return Results.Ok(forms);
});

app.MapPost("/api/feedback/submit", async ([FromQuery] Guid tenantId, [FromBody] SubmitFeedbackDto req, FeedbackService service) =>
{
    var res = await service.SubmitFeedbackAsync(tenantId, req.SurveyFormId, req.ContactId, req.Score, req.FeedbackText);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/api/feedback/responses", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var responses = await db.SurveyResponses.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.CreatedAtUtc).ToListAsync();
    return Results.Ok(responses);
});

// ----------------- Knowledge Base & RAG Endpoints -----------------
app.MapGet("/api/kb/articles", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var articles = await db.HelpArticles.Where(a => a.TenantId == tenantId && a.IsPublished).ToListAsync();
    return Results.Ok(articles);
});

app.MapPost("/api/kb/query", async ([FromQuery] Guid tenantId, [FromBody] KbQueryDto req, HelpCenterRagService ragService) =>
{
    var res = await ragService.GenerateAiDeflectionReplyAsync(tenantId, req.Question);
    return Results.Ok(res);
});

// ----------------- Identity 360 Endpoints -----------------
app.MapPost("/api/crm/resolve-contact", async ([FromQuery] Guid tenantId, [FromBody] ResolveContactDto req, IdentityResolverService resolver) =>
{
    var res = await resolver.ResolveOrCreateContactAsync(tenantId, req.Email, req.Phone, req.FirstName, req.LastName);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapGet("/api/crm/contacts/{id:guid}/timeline", async (Guid id, [FromQuery] Guid tenantId, IdentityResolverService resolver) =>
{
    var res = await resolver.GetUnifiedCustomerTimelineAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.NotFound(new { error = res.Error });
});

// ----------------- WhatsApp Templates & Broadcasts -----------------
app.MapGet("/api/channels/whatsapp/templates", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var templates = await db.WhatsAppTemplates.Where(t => t.TenantId == tenantId).ToListAsync();
    return Results.Ok(templates);
});

app.MapPost("/api/channels/whatsapp/templates", async ([FromBody] WhatsAppTemplate template, SGrowingDbContext db) =>
{
    db.WhatsAppTemplates.Add(template);
    await db.SaveChangesAsync();
    return Results.Ok(template);
});

app.MapGet("/api/channels/broadcasts", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var broadcasts = await db.BroadcastCampaigns.Where(b => b.TenantId == tenantId).ToListAsync();
    return Results.Ok(broadcasts);
});

app.MapPost("/api/channels/broadcasts", async ([FromBody] BroadcastCampaign campaign, SGrowingDbContext db) =>
{
    db.BroadcastCampaigns.Add(campaign);
    await db.SaveChangesAsync();
    return Results.Ok(campaign);
});

app.MapPost("/api/channels/broadcasts/{id:guid}/dispatch", async (Guid id, [FromQuery] Guid tenantId, BroadcastDispatcherService dispatcher) =>
{
    var res = await dispatcher.DispatchBroadcastAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// ----------------- In-Chat Booking & Assisted Checkout -----------------
app.MapGet("/api/sales/booking/slots", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var slots = await db.AppointmentSlots.Where(s => s.TenantId == tenantId && s.IsActive).ToListAsync();
    return Results.Ok(slots);
});

app.MapPost("/api/sales/booking/slots", async ([FromBody] AppointmentSlot slot, SGrowingDbContext db) =>
{
    db.AppointmentSlots.Add(slot);
    await db.SaveChangesAsync();
    return Results.Ok(slot);
});

app.MapPost("/api/sales/booking/book", async ([FromQuery] Guid tenantId, [FromBody] BookSlotDto req, AppointmentBookingService bookingService) =>
{
    var res = await bookingService.BookSlotAsync(tenantId, req.SlotId, req.ContactId, req.ScheduledTimeUtc);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/sales/checkout/create", async ([FromQuery] Guid tenantId, [FromBody] CreateCheckoutDto req, CheckoutLinkService checkoutService) =>
{
    var res = await checkoutService.CreateCheckoutLinkAsync(tenantId, req.ContactId, req.ConversationId, req.Title, req.Amount, req.Currency ?? "USD");
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

app.MapPost("/api/sales/checkout/{id:guid}/pay", async (Guid id, [FromQuery] Guid tenantId, CheckoutLinkService checkoutService) =>
{
    var res = await checkoutService.MarkPaidAsync(tenantId, id);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// ----------------- SLA Policies & Evaluation -----------------
app.MapGet("/api/sla/policies", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var policies = await db.SlaPolicies.Where(p => p.TenantId == tenantId).ToListAsync();
    return Results.Ok(policies);
});

app.MapPost("/api/sla/policies", async ([FromBody] SlaPolicy policy, SGrowingDbContext db) =>
{
    db.SlaPolicies.Add(policy);
    await db.SaveChangesAsync();
    return Results.Ok(policy);
});

app.MapPost("/api/sla/evaluate", async ([FromQuery] Guid tenantId, SlaMonitoringService slaService) =>
{
    var count = await slaService.EvaluateSlaBreachesAsync(tenantId);
    return Results.Ok(new { breachedCount = count });
});

// ----------------- Autonomous AI Agent -----------------
app.MapPost("/api/ai/agent/handle", async ([FromQuery] Guid tenantId, [FromBody] AutonomousAgentQueryDto req, AutonomousAiAgentService agentService) =>
{
    var res = await agentService.HandleIncomingMessageAsync(tenantId, req.ConversationId, req.ContactId, req.Message);
    return res.IsSuccess ? Results.Ok(res.Value) : Results.BadRequest(new { error = res.Error });
});

// ----------------- Voice & Telephony Endpoints -----------------
app.MapPost("/api/voice/inbound", async (HttpRequest request, [FromQuery] Guid tenantId, [FromQuery] string callSid, [FromQuery] string from, [FromQuery] string to, VoiceTelephonyService voiceService) =>
{
    var twiml = await voiceService.HandleInboundCallWebhookAsync(tenantId, callSid, from, to);
    return Results.Content(twiml, "application/xml");
});

app.MapPost("/api/voice/turn", async ([FromQuery] Guid tenantId, [FromBody] VoiceTurnDto req, RealtimeVoiceAgentService voiceAgent) =>
{
    var responseSpeech = await voiceAgent.ProcessVoiceTurnAsync(tenantId, req.SessionId, req.CallerSpeechTranscript);
    return Results.Ok(new { response = responseSpeech });
});

app.MapGet("/api/voice/sessions", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var sessions = await db.VoiceCallSessions.Where(s => s.TenantId == tenantId).OrderByDescending(s => s.CreatedAtUtc).ToListAsync();
    return Results.Ok(sessions);
});

// ----------------- Social Media Publishing & Deflection -----------------
app.MapGet("/api/social/posts", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var posts = await db.SocialPosts.Where(p => p.TenantId == tenantId).OrderByDescending(p => p.CreatedAtUtc).ToListAsync();
    return Results.Ok(posts);
});

app.MapPost("/api/social/posts", async ([FromQuery] Guid tenantId, [FromBody] CreateSocialPostDto req, SocialPublishingService publishingService) =>
{
    var post = await publishingService.CreatePostAsync(tenantId, req.Content, req.Platforms, req.ScheduledAtUtc, req.MediaUrls);
    return Results.Ok(post);
});

app.MapPost("/api/social/publish-due", async ([FromQuery] Guid tenantId, SocialPublishingService publishingService) =>
{
    var count = await publishingService.PublishDuePostsAsync(tenantId);
    return Results.Ok(new { publishedCount = count });
});

app.MapPost("/api/social/comments/deflect", async ([FromQuery] Guid tenantId, [FromBody] DeflectCommentDto req, SocialCommentDeflectionService deflectionService) =>
{
    var res = await deflectionService.HandlePublicCommentAsync(tenantId, req.Platform, req.PostExternalId, req.CommenterUsername, req.CommentText);
    return Results.Ok(res);
});

// ----------------- Multi-Touch AI SDR Outreach -----------------
app.MapPost("/api/outbound/multitouch/dispatch", async ([FromQuery] Guid tenantId, MultiTouchCadenceOrchestrator orchestrator) =>
{
    var count = await orchestrator.ProcessDueMultiTouchStepsAsync(tenantId);
    return Results.Ok(new { processed = count });
});

// ----------------- B2B CRM Accounts & Companies -----------------
app.MapGet("/api/crm/companies", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var companies = await db.Companies.Where(c => c.TenantId == tenantId).ToListAsync();
    return Results.Ok(companies);
});

app.MapPost("/api/crm/companies", async ([FromQuery] Guid tenantId, [FromBody] CreateCompanyDto req, CompanyAccountService companyService) =>
{
    var company = await companyService.CreateCompanyAsync(tenantId, req.Name, req.Domain, req.Industry, req.Revenue, req.ParentCompanyId);
    return Results.Ok(company);
});

app.MapPost("/api/crm/companies/associate", async ([FromQuery] Guid tenantId, [FromBody] AssociateCompanyDto req, CompanyAccountService companyService) =>
{
    var company = await companyService.AssociateContactByDomainAsync(tenantId, req.ContactId, req.ContactEmail);
    return company != null ? Results.Ok(company) : Results.NotFound(new { error = "No company found matching contact email domain." });
});

// ----------------- Visual Node Workflows -----------------
app.MapGet("/api/workflows", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var workflows = await db.WorkflowGraphs.Where(w => w.TenantId == tenantId).ToListAsync();
    return Results.Ok(workflows);
});

app.MapPost("/api/workflows", async ([FromBody] WorkflowGraph graph, SGrowingDbContext db) =>
{
    db.WorkflowGraphs.Add(graph);
    await db.SaveChangesAsync();
    return Results.Ok(graph);
});

app.MapPost("/api/workflows/{id:guid}/trigger", async (Guid id, [FromQuery] Guid tenantId, [FromBody] TriggerWorkflowDto req, WorkflowEngineService engine) =>
{
    var execution = await engine.ExecuteWorkflowAsync(tenantId, id, req.InputPayloadJson);
    return Results.Ok(execution);
});

// ----------------- Real-Time Streaming Voice Pipeline -----------------
app.MapPost("/api/voice/streaming/frame", ([FromBody] StreamAudioChunkDto req, StreamingVoiceWebSocketHandler handler, SGrowingDbContext db) =>
{
    var session = db.StreamingVoiceCallSessions.FirstOrDefault(s => s.TenantId == req.TenantId && s.Id == req.SessionId)
                  ?? StreamingVoiceCallSession.Create(req.TenantId, req.CallSid, req.StreamSid);

    if (!db.StreamingVoiceCallSessions.Contains(session))
    {
        db.StreamingVoiceCallSessions.Add(session);
    }

    var result = handler.ProcessInboundAudioChunk(session, req.PayloadBase64, req.SequenceNumber);
    db.SaveChanges();

    return Results.Ok(result);
});

app.MapPost("/api/voice/streaming/transfer", ([FromBody] WarmTransferDto req, BargeInInterruptionService transferService, SGrowingDbContext db) =>
{
    var session = db.StreamingVoiceCallSessions.FirstOrDefault(s => s.TenantId == req.TenantId && s.Id == req.SessionId)
                  ?? StreamingVoiceCallSession.Create(req.TenantId, "CA_MOCK", "MZ_MOCK");

    var result = transferService.InitiateWarmSipTransfer(session, req.TargetSipUri, req.Reason, req.Summary);
    db.SaveChanges();

    return Results.Ok(result);
});

// ----------------- PostgreSQL pgvector RAG & Hybrid Search -----------------
app.MapPost("/api/rag/documents/index", async ([FromBody] IndexDocumentDto req, VectorRagRetrievalService ragService) =>
{
    var chunks = await ragService.IndexDocumentAsync(req.TenantId, req.DocumentId, req.Title, req.Content, req.SourceUrl);
    return Results.Ok(new { indexedChunks = chunks.Count, documentId = req.DocumentId });
});

app.MapPost("/api/rag/search", async ([FromBody] SearchRagDto req, VectorRagRetrievalService ragService) =>
{
    var results = await ragService.HybridSearchAsync(req.TenantId, req.Query, req.TopK);
    return Results.Ok(results);
});

app.MapPost("/api/rag/grounded-context", async ([FromBody] SearchRagDto req, VectorRagRetrievalService ragService) =>
{
    var context = await ragService.GenerateGroundedContextAsync(req.TenantId, req.Query, req.TopK);
    return Results.Ok(context);
});

// ----------------- B2B Lead Enrichment & In-Chat E-Signatures -----------------
app.MapPost("/api/crm/leads/enrich", async ([FromBody] EnrichLeadDto req, LeadEnrichmentService enrichmentService) =>
{
    var profile = await enrichmentService.EnrichLeadAsync(req.TenantId, req.DomainOrEmail);
    return Results.Ok(profile);
});

app.MapPost("/api/crm/contracts", async ([FromBody] CreateContractDto req, ContractSigningService contractService) =>
{
    var contract = await contractService.CreateContractAsync(
        req.TenantId, req.Title, req.ContractType, req.SignerName, req.SignerEmail, req.ContractValue, req.CompanyId, req.DealId);
    return Results.Ok(contract);
});

app.MapPost("/api/crm/contracts/{id:guid}/dispatch", async (Guid id, [FromQuery] Guid tenantId, ContractSigningService contractService) =>
{
    var dispatch = await contractService.DispatchInChatContractAsync(tenantId, id);
    return Results.Ok(dispatch);
});

app.MapGet("/api/crm/contracts/sign/{token}", async (string token, ContractSigningService contractService) =>
{
    var contract = await contractService.GetAndMarkViewedAsync(token);
    return contract != null ? Results.Ok(contract) : Results.NotFound();
});

app.MapPost("/api/crm/contracts/sign/{token}", async (string token, [FromBody] SignContractDto req, ContractSigningService contractService) =>
{
    var result = await contractService.CompleteSigningAsync(token, req.SignerSignatureName, req.SignedPdfUrl);
    return Results.Ok(result);
});

// ----------------- PIM Catalog & In-Chat CPQ Engine -----------------
app.MapPost("/api/commerce/products", async ([FromBody] CreateProductDto req, ProductCatalogService catalog) =>
{
    var product = await catalog.CreateProductAsync(req.TenantId, req.Name, req.Slug, req.Description, req.ProductType, req.BasePrice, req.Currency ?? "USD");
    return Results.Ok(product);
});

app.MapGet("/api/commerce/products", async ([FromQuery] Guid tenantId, ProductCatalogService catalog) =>
{
    var products = await catalog.GetProductsAsync(tenantId);
    return Results.Ok(products);
});

app.MapPost("/api/commerce/products/{id:guid}/variants", async (Guid id, [FromBody] CreateVariantDto req, ProductCatalogService catalog) =>
{
    var variant = await catalog.AddVariantAsync(req.TenantId, id, req.Sku, req.Name, req.Price, req.InventoryStock, req.AttributesJson ?? "{}");
    return Results.Ok(variant);
});

app.MapPost("/api/commerce/quotes", async ([FromBody] CreateQuoteDto req, InChatCpqService cpq) =>
{
    var quote = await cpq.CreateQuoteAsync(req.TenantId, req.ContactId, req.Items, req.CompanyId, req.DealId, req.DiscountPercent, req.TaxPercent, req.Currency ?? "USD", req.Notes);
    return Results.Ok(quote);
});

app.MapPost("/api/commerce/quotes/{id:guid}/message", async (Guid id, [FromQuery] Guid tenantId, InChatCpqService cpq) =>
{
    var message = await cpq.GenerateInChatQuoteMessageAsync(tenantId, id);
    return Results.Ok(new { formattedMessage = message });
});

app.MapPost("/api/commerce/quotes/{id:guid}/accept", async (Guid id, [FromQuery] Guid tenantId, InChatCpqService cpq) =>
{
    var quote = await cpq.AcceptQuoteAsync(tenantId, id);
    return Results.Ok(quote);
});

app.MapPost("/api/webhooks/stripe", async ([FromQuery] Guid tenantId, [FromBody] JsonElement payload, StripePaymentWebhookHandler stripeHandler) =>
{
    var result = await stripeHandler.ProcessStripeEventAsync(tenantId, payload.ToString());
    return Results.Ok(result);
});

// ----------------- Storefront Mesh & Abandoned Cart Recovery -----------------
app.MapPost("/api/integrations/shopify/webhook", async ([FromQuery] Guid tenantId, [FromHeader(Name = "X-Shopify-Topic")] string? topic, [FromBody] JsonElement payload, ShopifyWebhookReceiver receiver) =>
{
    var result = await receiver.ProcessWebhookAsync(tenantId, topic ?? "checkouts/create", payload.ToString());
    return Results.Ok(result);
});

app.MapPost("/api/integrations/carts/abandoned/scan", async ([FromQuery] Guid tenantId, AbandonedCartRecoveryService recoveryService) =>
{
    var results = await recoveryService.TriggerRecoveryScanAsync(tenantId);
    return Results.Ok(results);
});

// ----------------- Voice Realtime Bridge & Softphone -----------------
app.MapPost("/api/voice/realtime/session-config", ([FromBody] RealtimeSessionConfig config, OpenAiRealtimeBridgeService bridge) =>
{
    var sessionCmd = bridge.BuildSessionUpdateCommand(config);
    return Results.Ok(new { commandJson = sessionCmd });
});

app.MapGet("/api/voice/softphone/config", ([FromQuery] Guid tenantId, [FromQuery] Guid userId, BrowserSoftphoneSignalingService softphone) =>
{
    var config = softphone.CreateSessionConfig(tenantId, userId);
    return Results.Ok(config);
});

// ----------------- Developer Webhooks & GDPR Privacy -----------------
app.MapPost("/api/developer/webhooks/subscriptions", async ([FromBody] CreateWebhookSubscriptionDto req, SGrowingDbContext db) =>
{
    var sub = WebhookSubscription.Create(req.TenantId, req.TargetUrl, req.SecretToken, req.SubscribedEvents ?? "*");
    await db.WebhookSubscriptions.AddAsync(sub);
    await db.SaveChangesAsync();
    return Results.Ok(sub);
});

app.MapGet("/api/developer/webhooks/subscriptions", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var subs = await db.WebhookSubscriptions.Where(s => s.TenantId == tenantId).ToListAsync();
    return Results.Ok(subs);
});

app.MapPost("/api/privacy/erasure", async ([FromBody] PrivacyErasureDto req, CustomerPrivacyService privacyService) =>
{
    var result = await privacyService.ExecuteRightToBeForgottenAsync(req.TenantId, req.ContactEmail);
    return Results.Ok(result);
});

// ----------------- Multi-Touch Revenue Attribution & BI -----------------
app.MapPost("/api/analytics/touchpoints", async ([FromBody] RecordTouchpointDto req, MultiTouchAttributionService attribution) =>
{
    var tp = await attribution.RecordTouchpointAsync(req.TenantId, req.ContactId, req.Channel, req.CampaignName, req.Stage, req.DealId, req.Cost, req.UtmSource, req.UtmMedium, req.UtmCampaign);
    return Results.Ok(tp);
});

app.MapPost("/api/analytics/attribution/calculate", async ([FromBody] CalculateAttributionDto req, MultiTouchAttributionService attribution) =>
{
    var results = await attribution.CalculateAttributionAsync(req.TenantId, req.DealId, req.Model);
    return Results.Ok(results);
});

app.MapGet("/api/analytics/kpis", async ([FromQuery] Guid tenantId, MultiTouchAttributionService attribution) =>
{
    var kpis = await attribution.CalculateExecutiveKpisAsync(tenantId);
    return Results.Ok(kpis);
});

// ----------------- SaaS Usage Metering & Plan Entitlements -----------------
app.MapGet("/api/saas/usage/summary", async ([FromQuery] Guid tenantId, TenantUsageMeteringService metering) =>
{
    var summary = await metering.GetUsageSummaryAsync(tenantId);
    return Results.Ok(summary);
});

app.MapPost("/api/saas/usage/record", async ([FromBody] RecordUsageDto req, TenantUsageMeteringService metering) =>
{
    var record = await metering.RecordUsageAsync(req.TenantId, req.Metric, req.Quantity, req.Metadata);
    return Results.Ok(record);
});

app.MapPost("/api/saas/entitlements/check", async ([FromBody] CheckEntitlementDto req, TenantUsageMeteringService metering) =>
{
    var check = await metering.CheckEntitlementAsync(req.TenantId, req.Metric, req.IncomingQuantity);
    return Results.Ok(check);
});

// ----------------- Advanced Workflows & Quiet Hours -----------------
app.MapGet("/api/workflows/dlq", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var dlq = await db.WorkflowDeadLetterLogs.Where(d => d.TenantId == tenantId).ToListAsync();
    return Results.Ok(dlq);
});

app.MapPost("/api/workflows/quiet-hours/evaluate", async ([FromBody] EvaluateQuietHoursDto req, QuietHoursGuardService guard) =>
{
    var result = await guard.EvaluateDispatchTimeAsync(req.TenantId, req.RecipientLocalTime, req.IsUrgent);
    return Results.Ok(result);
});

// ----------------- Enterprise AI Gateway & Prompt Firewall -----------------
app.MapPost("/api/ai/firewall/inspect", ([FromBody] InspectPromptDto req, PromptFirewallService firewall) =>
{
    var result = firewall.InspectAndSanitize(req.UserPrompt);
    return Results.Ok(result);
});

app.MapPost("/api/ai/gateway/route", ([FromBody] RouteModelDto req, AiModelRouterService router) =>
{
    var decision = router.SelectModelForTask(req.Complexity, req.TaskHint);
    return Results.Ok(decision);
});

app.MapPost("/api/ai/tools/register", async ([FromBody] RegisterToolDto req, DynamicAgentToolRegistry toolRegistry) =>
{
    var tool = await toolRegistry.RegisterToolAsync(req.TenantId, req.Name, req.Description, req.SchemaJson, req.TargetUrl, req.HttpMethod ?? "POST");
    return Results.Ok(tool);
});

app.MapGet("/api/ai/tools", async ([FromQuery] Guid tenantId, DynamicAgentToolRegistry toolRegistry) =>
{
    var tools = await toolRegistry.GetActiveToolsAsync(tenantId);
    return Results.Ok(tools);
});

app.MapPost("/api/ai/tools/execute", async ([FromBody] ExecuteToolDto req, DynamicAgentToolRegistry toolRegistry) =>
{
    var result = await toolRegistry.ExecuteToolAsync(req.TenantId, req.ToolName, req.ArgumentsJson);
    return Results.Ok(result);
});

// --- COD Order Confirmation & Anti-Fraud Endpoints ---
app.MapPost("/api/cod/orders", async ([FromBody] CreateCodOrderDto req, SGrowingDbContext db, ICodConfirmationService codService) =>
{
    var order = CodOrder.Create(req.TenantId, req.OrderNumber, req.CustomerName, req.PhoneNumber, req.ShippingAddress, req.GovernorateOrWilaya, req.DelegationOrCommune, req.TotalAmount, req.CountryCode ?? "TN", req.CodCurrency ?? "TND", req.ShippingFee, req.AlternatePhone, req.ItemsJson ?? "[]", req.Notes);
    db.CodOrders.Add(order);
    await db.SaveChangesAsync();
    var risk = await codService.EvaluateRiskAsync(req.TenantId, order);
    return Results.Ok(new { Order = order, Risk = risk });
});

app.MapGet("/api/cod/orders", async ([FromQuery] Guid tenantId, SGrowingDbContext db) =>
{
    var orders = await db.CodOrders.Where(o => o.TenantId == tenantId).OrderByDescending(o => o.CreatedAtUtc).ToListAsync();
    return Results.Ok(orders);
});

app.MapPost("/api/cod/orders/lock", async ([FromBody] AcquireLockDto req, ICodConfirmationService codService) =>
{
    var result = await codService.AcquireLockAsync(req.TenantId, req.OrderId, req.AgentId, req.AgentName, req.LeaseMinutes <= 0 ? 3 : req.LeaseMinutes);
    return Results.Ok(result);
});

app.MapPost("/api/cod/orders/release-lock", async ([FromBody] ReleaseLockDto req, ICodConfirmationService codService) =>
{
    var success = await codService.ReleaseLockAsync(req.TenantId, req.OrderId, req.AgentId);
    return Results.Ok(new { Success = success });
});

app.MapPost("/api/cod/orders/call-outcome", async ([FromBody] RecordOutcomeDto req, ICodConfirmationService codService) =>
{
    var order = await codService.RecordCallOutcomeAsync(req.TenantId, req.OrderId, req.AgentId, req.AgentName, req.Outcome, req.Notes, req.ScheduledCallbackUtc);
    return Results.Ok(order);
});

app.MapPost("/api/cod/orders/interactive-wa", async ([FromBody] SendWaInteractiveDto req, ICodConfirmationService codService) =>
{
    var payload = await codService.GenerateInteractiveWhatsAppPayloadAsync(req.TenantId, req.OrderId);
    return Results.Ok(new { Payload = payload });
});

app.MapPost("/api/cod/orders/interactive-response", async ([FromBody] HandleWaResponseDto req, ICodConfirmationService codService) =>
{
    var order = await codService.HandleCustomerInteractiveResponseAsync(req.TenantId, req.OrderId, req.Action);
    return Results.Ok(order);
});

// --- Logistics & Manifests Endpoints ---
app.MapPost("/api/logistics/dispatch", async ([FromBody] DispatchShipmentDto req, ICourierLogisticsHubService logisticsHub) =>
{
    var shipment = await logisticsHub.DispatchOrderAsync(req.TenantId, req.OrderId, req.CourierProviderId);
    return Results.Ok(shipment);
});

app.MapPost("/api/logistics/manifests", async ([FromBody] GenerateManifestDto req, ICourierLogisticsHubService logisticsHub) =>
{
    var manifest = await logisticsHub.GenerateBordereauManifestAsync(req.TenantId, req.CourierProviderId, req.OrderIds);
    return Results.Ok(manifest);
});

app.MapPost("/api/logistics/webhooks/tracking", async ([FromBody] IngestCourierWebhookDto req, ICourierLogisticsHubService logisticsHub) =>
{
    var shipment = await logisticsHub.ProcessTrackingWebhookAsync(req.TenantId, req.TrackingNumber, req.Status, req.CashRemitted, req.Notes);
    return Results.Ok(shipment);
});

// --- Defensive WMS Endpoints ---
app.MapGet("/api/wms/inventory", async ([FromQuery] Guid tenantId, [FromQuery] Guid warehouseId, [FromQuery] string sku, IDefensiveWmsService wmsService) =>
{
    var summary = await wmsService.GetStockSummaryAsync(tenantId, warehouseId, sku);
    return Results.Ok(summary);
});

app.MapPost("/api/wms/reserve", async ([FromBody] WmsStockActionDto req, IDefensiveWmsService wmsService) =>
{
    var inv = await wmsService.ReserveStockOnCheckoutAsync(req.TenantId, req.WarehouseId, req.Sku, req.Quantity, req.OrderId, req.PerformedBy ?? "Checkout");
    return Results.Ok(inv);
});

app.MapPost("/api/wms/commit", async ([FromBody] WmsStockActionDto req, IDefensiveWmsService wmsService) =>
{
    var inv = await wmsService.CommitStockOnConfirmationAsync(req.TenantId, req.WarehouseId, req.Sku, req.Quantity, req.OrderId, req.PerformedBy ?? "ConfirmationAgent");
    return Results.Ok(inv);
});

app.MapPost("/api/wms/dispatch", async ([FromBody] WmsStockActionDto req, IDefensiveWmsService wmsService) =>
{
    var inv = await wmsService.DispatchStockAsync(req.TenantId, req.WarehouseId, req.Sku, req.Quantity, req.OrderId, req.PerformedBy ?? "LogisticsCourier");
    return Results.Ok(inv);
});

app.MapPost("/api/wms/quarantine/restock", async ([FromBody] WmsQuarantineRestockDto req, IDefensiveWmsService wmsService) =>
{
    var inv = await wmsService.ScanAndRestockQuarantineItemAsync(req.TenantId, req.WarehouseId, req.Sku, req.Quantity, req.IsDamaged, req.InspectorId ?? "WarehouseInspector");
    return Results.Ok(inv);
});

// --- Sales Cadences & Stop-on-Reply Endpoints ---
app.MapPost("/api/cadences", async ([FromBody] CreateCadenceDto req, ISalesCadenceEngine cadenceEngine) =>
{
    var cadence = await cadenceEngine.CreateCadenceAsync(req.TenantId, req.Name, req.Description, req.TargetAudience);
    return Results.Ok(cadence);
});

app.MapPost("/api/cadences/{id:guid}/steps", async (Guid id, [FromBody] AddCadenceStepDto req, ISalesCadenceEngine cadenceEngine) =>
{
    var step = await cadenceEngine.AddCadenceStepAsync(id, req.StepNumber, req.DayOffset, req.Channel, req.SubjectTemplate, req.BodyTemplate);
    return Results.Ok(step);
});

app.MapPost("/api/cadences/enroll", async ([FromBody] EnrollCadenceLeadDto req, ISalesCadenceEngine cadenceEngine) =>
{
    var enrollment = await cadenceEngine.EnrollLeadAsync(req.TenantId, req.CadenceId, req.LeadName, req.Email, req.PhoneNumber, req.Company);
    return Results.Ok(enrollment);
});

app.MapPost("/api/cadences/process-due", async ([FromBody] ProcessDueStepsDto req, ISalesCadenceEngine cadenceEngine) =>
{
    var count = await cadenceEngine.ProcessDueCadenceStepsAsync(req.TenantId);
    return Results.Ok(new { ProcessedCount = count });
});

app.MapPost("/api/cadences/reply-stop", async ([FromBody] CadenceReplyStopDto req, ISalesCadenceEngine cadenceEngine) =>
{
    var stopped = await cadenceEngine.HandleProspectReplyAsync(req.TenantId, req.EmailOrPhone, req.Channel, req.ReplyText);
    return Results.Ok(new { Stopped = stopped });
});

app.MapGet("/api/cadences/sdr-tasks", async ([FromQuery] Guid tenantId, ISalesCadenceEngine cadenceEngine) =>
{
    var tasks = await cadenceEngine.GetPendingSdrTasksAsync(tenantId);
    return Results.Ok(tasks);
});

app.MapPost("/api/cadences/sdr-tasks/{id:guid}/complete", async (Guid id, [FromBody] CompleteTaskDto req, ISalesCadenceEngine cadenceEngine) =>
{
    var task = await cadenceEngine.CompleteSdrTaskAsync(req.TenantId, id, req.Outcome);
    return Results.Ok(task);
});

// --- Customer 360 & RMA Endpoints ---
app.MapPost("/api/crm/customers/resolve", async ([FromBody] ResolveCustomerDto req, ICustomerIdentityGraphService idGraph) =>
{
    var profile = await idGraph.ResolveCustomerAsync(req.TenantId, req.Name, req.Phone, req.Email, req.ChannelType, req.ChannelValue);
    return Results.Ok(profile);
});

app.MapGet("/api/crm/customers/{id:guid}/360", async (Guid id, [FromQuery] Guid tenantId, ICustomerIdentityGraphService idGraph) =>
{
    var detail = await idGraph.GetCustomer360Async(tenantId, id);
    return Results.Ok(detail);
});

app.MapPost("/api/crm/customers/merge", async ([FromBody] MergeCustomersDto req, ICustomerIdentityGraphService idGraph) =>
{
    var merged = await idGraph.MergeCustomerProfilesAsync(req.TenantId, req.PrimaryCustomerId, req.DuplicateCustomerId, req.PerformedBy ?? "Admin");
    return Results.Ok(merged);
});

app.MapPost("/api/portal/tracking", async ([FromBody] PublicTrackingDto req, ICustomerSelfServicePortalService portalService) =>
{
    var tracking = await portalService.LookupTrackingAsync(req.OrderNumber, req.Phone);
    return Results.Ok(tracking);
});

app.MapPost("/api/portal/rma/submit", async ([FromBody] SubmitRmaDto req, ICustomerSelfServicePortalService portalService) =>
{
    var rma = await portalService.SubmitRmaRequestAsync(req.TenantId, req.OrderNumber, req.Phone, req.Reason, req.Notes, req.PhotoUrls);
    return Results.Ok(rma);
});

app.MapPost("/api/portal/rma/review", async ([FromBody] ReviewRmaDto req, ICustomerSelfServicePortalService portalService) =>
{
    var rma = await portalService.ReviewRmaRequestAsync(req.TenantId, req.RmaId, req.IsApproved, req.ResolutionNotes, req.DispatchReverseCourier, req.CourierId);
    return Results.Ok(rma);
});

// --- Server-Side Conversion Mesh Endpoints ---
app.MapPost("/api/tracking/conversions", async ([FromBody] TrackConversionDto req, IServerConversionMeshService mesh) =>
{
    var ev = await mesh.TrackConversionAsync(req.TenantId, req.Platform, req.EventType, req.ValueAmount, req.Currency ?? "USD", req.Email, req.Phone, req.FirstName, req.LastName, req.City, req.Country, req.Fbc, req.Fbp, req.UserAgent, req.IpAddress, req.CustomDataJson, req.EventId);
    var emq = mesh.CalculateEventMatchQualityScore(ev);
    return Results.Ok(new { Event = ev, MatchQualityScore = emq });
});

app.MapGet("/api/tracking/conversions", async ([FromQuery] Guid tenantId, [FromQuery] int limit, IServerConversionMeshService mesh) =>
{
    var events = await mesh.GetRecentConversionsAsync(tenantId, limit <= 0 ? 50 : limit);
    return Results.Ok(events);
});

// --- Courier Cash Remittance & Financial Clearing Endpoints ---
app.MapPost("/api/remittance/reconcile", async ([FromBody] ReconcileRemittanceDto req, ICourierRemittanceClearingService clearingService) =>
{
    var result = await clearingService.ReconcileStatementAsync(req.TenantId, req.CourierProviderId, req.StatementReference, req.Items, req.ReconciledBy ?? "FinanceManager");
    return Results.Ok(result);
});

app.MapPost("/api/remittance/discrepancies/resolve", async ([FromBody] ResolveDiscrepancyDto req, ICourierRemittanceClearingService clearingService) =>
{
    var match = await clearingService.ResolveDiscrepancyAsync(req.TenantId, req.MatchId, req.ResolutionNotes);
    return Results.Ok(match);
});

app.MapGet("/api/remittance/profitability", async ([FromQuery] Guid tenantId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate, [FromQuery] decimal cogsPercent, ICourierRemittanceClearingService clearingService) =>
{
    var summary = await clearingService.CalculateNetProfitabilityAsync(tenantId, startDate, endDate, cogsPercent <= 0 ? 35.0m : cogsPercent);
    return Results.Ok(summary);
});

// --- In-Chat Commerce & Cart Builder Endpoints ---
app.MapPost("/api/chat-commerce/parse", ([FromBody] ParseChatCartDto req, IConversationalCartEngine cartEngine) =>
{
    var intent = cartEngine.ParseCartIntent(req.MessageText, req.DefaultUnitPrice <= 0 ? 65.0m : req.DefaultUnitPrice, req.Currency ?? "TND");
    return Results.Ok(intent);
});

app.MapPost("/api/chat-commerce/orders", async ([FromBody] CreateChatOrderDto req, IConversationalCartEngine cartEngine) =>
{
    var result = await cartEngine.CreateInChatOrderAsync(req.TenantId, req.ConversationId, req.Intent, req.WarehouseId);
    return Results.Ok(result);
});

// --- External Storefront & Sheets Mesh Endpoints ---
app.MapPost("/api/storefront/shopify/webhook", async ([FromQuery] Guid tenantId, [FromBody] JsonElement payload, IExternalStoreAndSheetsService storeService) =>
{
    var order = await storeService.IngestExternalStoreOrderAsync(tenantId, ExternalStorePlatform.Shopify, payload.GetRawText());
    return Results.Ok(order);
});

app.MapPost("/api/storefront/shopify/fulfill", async ([FromBody] PushFulfillmentDto req, IExternalStoreAndSheetsService storeService) =>
{
    var record = await storeService.PushFulfillmentUpdateAsync(req.TenantId, req.OrderId, req.CourierName, req.TrackingNumber);
    return Results.Ok(record);
});

app.MapPost("/api/storefront/sheets/row", async ([FromBody] GenerateSheetsRowDto req, IExternalStoreAndSheetsService storeService) =>
{
    var row = await storeService.GenerateGoogleSheetsRowAsync(req.TenantId, req.OrderId);
    return Results.Ok(new { Row = row });
});

// --- Autonomous Voice AI Dialer Endpoints ---
app.MapPost("/api/voice/dialer/queue", async ([FromBody] QueueVoiceOrderDto req, IAutomatedVoiceDialerService dialerService) =>
{
    var attempt = await dialerService.QueueOrderForVoiceConfirmationAsync(req.TenantId, req.OrderId, req.CampaignId);
    return Results.Ok(attempt);
});

app.MapPost("/api/voice/dialer/process-turn", async ([FromBody] ProcessVoiceTurnDto req, IAutomatedVoiceDialerService dialerService) =>
{
    var attempt = await dialerService.ProcessIvrCustomerResponseAsync(req.TenantId, req.AttemptId, req.SpeechText, req.DtmfDigit);
    return Results.Ok(attempt);
});

// --- Barcode Fulfillment Desk Endpoints ---
app.MapPost("/api/fulfillment/pickslips", async ([FromBody] CreatePickSlipDto req, IBarcodeFulfillmentService fulfillmentService) =>
{
    var slip = await fulfillmentService.CreatePickSlipAsync(req.TenantId, req.OrderId, req.WarehouseId);
    return Results.Ok(slip);
});

app.MapPost("/api/fulfillment/pickslips/scan", async ([FromBody] ScanPickItemDto req, IBarcodeFulfillmentService fulfillmentService) =>
{
    var result = await fulfillmentService.ScanItemToPickSlipAsync(req.TenantId, req.PickSlipId, req.ScannedBarcodeOrSku);
    return Results.Ok(result);
});

app.MapPost("/api/fulfillment/pickslips/complete-pack", async ([FromBody] CompletePackDto req, IBarcodeFulfillmentService fulfillmentService) =>
{
    var slip = await fulfillmentService.CompletePackAndAssignAwbAsync(req.TenantId, req.PickSlipId, req.TrackingNumber, req.CourierName);
    return Results.Ok(slip);
});

app.MapPost("/api/fulfillment/manifests/scan", async ([FromBody] ScanManifestParcelDto req, IBarcodeFulfillmentService fulfillmentService) =>
{
    var manifest = await fulfillmentService.ScanParcelToManifestAsync(req.TenantId, req.ManifestId, req.TrackingNumber);
    return Results.Ok(manifest);
});

app.MapPost("/api/fulfillment/manifests/close-sign", async ([FromBody] CloseManifestDto req, IBarcodeFulfillmentService fulfillmentService) =>
{
    var manifest = await fulfillmentService.CloseAndSignManifestAsync(req.TenantId, req.ManifestId, req.DriverName, req.DriverPhone, req.DriverSignature);
    return Results.Ok(manifest);
});

app.MapPost("/api/fulfillment/rma/inspect", async ([FromBody] InspectRmaParcelDto req, IBarcodeFulfillmentService fulfillmentService) =>
{
    var rma = await fulfillmentService.InspectAndRestockRmaParcelAsync(req.TenantId, req.TrackingNumber, req.InspectorId, req.Condition, req.Notes, req.WarehouseId);
    return Results.Ok(rma);
});

// --- WhatsApp Interactive Catalog & Inbound Cart Endpoints ---
app.MapPost("/api/channels/whatsapp/catalog/sync", async ([FromBody] SyncMetaCatalogDto req, IWhatsAppCatalogEngine catalogEngine) =>
{
    var feed = await catalogEngine.SyncProductsToMetaCatalogAsync(req.TenantId, req.CatalogId);
    return Results.Ok(feed);
});

app.MapGet("/api/channels/whatsapp/catalog/feed", async ([FromQuery] Guid tenantId, IWhatsAppCatalogEngine catalogEngine) =>
{
    var feedJson = await catalogEngine.GenerateMetaCatalogFeedJsonAsync(tenantId);
    return Results.Content(feedJson, "application/json");
});

app.MapPost("/api/channels/whatsapp/catalog/order-webhook", async ([FromBody] WhatsAppInboundOrderWebhookDto req, IWhatsAppCatalogEngine catalogEngine) =>
{
    var order = await catalogEngine.ProcessInboundWhatsAppOrderWebhookAsync(req.TenantId, req.CustomerPhone, req.CustomerName, req.CatalogId, req.Items, req.ShippingAddress, req.ShippingGovernorate);
    return Results.Ok(order);
});

// --- Storefront White-Labeling & Custom Domains Endpoints ---
app.MapPost("/api/storefronts/domains/register", async ([FromBody] RegisterDomainDto req, IStorefrontDomainService domainService) =>
{
    var domain = await domainService.RegisterCustomDomainAsync(req.TenantId, req.Hostname, req.IsPrimary);
    return Results.Ok(domain);
});

app.MapPost("/api/storefronts/domains/verify-dns", async ([FromBody] VerifyDomainDnsDto req, IStorefrontDomainService domainService) =>
{
    var domain = await domainService.VerifyDomainDnsAsync(req.TenantId, req.DomainId, req.SimulateValidDns);
    return Results.Ok(domain);
});

app.MapPost("/api/storefronts/branding/tokens", async ([FromBody] UpsertBrandTokensDto req, IStorefrontDomainService domainService) =>
{
    var tokens = new BrandThemeTokens
    {
        StorefrontSlug = req.StorefrontSlug,
        BrandName = req.BrandName,
        PrimaryColorHex = req.PrimaryColorHex,
        SecondaryColorHex = req.SecondaryColorHex,
        FontFamily = req.FontFamily,
        LogoUrl = req.LogoUrl,
        FaviconUrl = req.FaviconUrl,
        SupportEmail = req.SupportEmail,
        SupportPhone = req.SupportPhone,
        SmsSenderId = req.SmsSenderId,
        WhatsAppSenderPhone = req.WhatsAppSenderPhone
    };
    var result = await domainService.UpsertBrandThemeTokensAsync(req.TenantId, tokens);
    return Results.Ok(result);
});

app.MapGet("/api/v1/public/storefront/{slugOrDomain}", async (string slugOrDomain, IStorefrontDomainService domainService) =>
{
    var config = await domainService.GetPublicStorefrontConfigAsync(slugOrDomain);
    return config != null ? Results.Ok(config) : Results.NotFound();
});

// --- Customer Reputation & Micro-Deposit Gateway Endpoints ---
app.MapGet("/api/commerce/reputation/evaluate", async ([FromQuery] Guid tenantId, [FromQuery] string phone, [FromQuery] string? email, ICustomerReputationService repService) =>
{
    var result = await repService.EvaluateCustomerReputationAsync(tenantId, phone, email);
    return Results.Ok(result);
});

app.MapPost("/api/commerce/reputation/deposit-link", async ([FromBody] GenerateDepositLinkDto req, ICustomerReputationService repService) =>
{
    var link = await repService.GenerateMicroDepositLinkAsync(req.TenantId, req.OrderId, req.CustomerPhone, req.DepositAmount, req.Provider);
    return Results.Ok(link);
});

app.MapPost("/api/commerce/reputation/settle-deposit", async ([FromBody] SettleDepositDto req, ICustomerReputationService repService) =>
{
    var order = await repService.RecordMicroDepositSettlementAsync(req.TenantId, req.LinkId);
    return Results.Ok(order);
});

// --- Smart Courier Routing Endpoints ---
app.MapGet("/api/commerce/routing/rank", async ([FromQuery] Guid tenantId, [FromQuery] string governorate, [FromQuery] decimal weight, [FromQuery] decimal codAmount, ISmartCourierRoutingService routingService) =>
{
    var recommendations = await routingService.RankCarriersForOrderAsync(tenantId, governorate, weight, codAmount);
    return Results.Ok(recommendations);
});

app.MapPost("/api/commerce/routing/auto-assign", async ([FromBody] AutoAssignCarrierDto req, ISmartCourierRoutingService routingService) =>
{
    var optimal = await routingService.AutoAssignOptimalCarrierAsync(req.TenantId, req.OrderId, req.PackageWeightKg);
    return Results.Ok(optimal);
});

app.MapPost("/api/commerce/routing/sla-breach-fallback", async ([FromBody] SlaBreachFallbackDto req, ISmartCourierRoutingService routingService) =>
{
    var order = await routingService.HandlePickupSlaBreachFallbackAsync(req.TenantId, req.CurrentTrackingNumber);
    return Results.Ok(order);
});

// --- Post-Purchase Retention & Replenishment Endpoints ---
app.MapPost("/api/marketing/retention/schedule", async ([FromBody] ScheduleRetentionDto req, IPostPurchaseRetentionService retentionService) =>
{
    var schedule = await retentionService.SchedulePostPurchaseRetentionAsync(req.TenantId, req.OrderId);
    return Results.Ok(schedule);
});

app.MapPost("/api/marketing/retention/survey-response", async ([FromBody] SubmitRetentionSurveyDto req, IPostPurchaseRetentionService retentionService) =>
{
    var survey = await retentionService.RecordSurveyResponseAsync(req.TenantId, req.SurveyId, req.Rating, req.Notes);
    return Results.Ok(survey);
});

app.MapPost("/api/marketing/retention/process-replenishments", async ([FromBody] ProcessReplenishmentsDto req, IPostPurchaseRetentionService retentionService) =>
{
    var results = await retentionService.ProcessDueReplenishmentRemindersAsync(req.TenantId, req.CurrentTimeUtc);
    return Results.Ok(results);
});

app.MapGet("/api/marketing/retention/win-back", async ([FromQuery] Guid tenantId, [FromQuery] int inactiveDays, IPostPurchaseRetentionService retentionService) =>
{
    var results = await retentionService.ProcessWinBackCampaignAsync(tenantId, inactiveDays);
    return Results.Ok(results);
});

// --- Multi-Warehouse Proximity & Inter-Hub Transfers Endpoints ---
app.MapGet("/api/warehouses/proximity-allocate", async ([FromQuery] Guid tenantId, [FromQuery] string governorate, [FromQuery] string sku, [FromQuery] int quantity, IMultiWarehouseService whService) =>
{
    var allocation = await whService.AllocateOrderToOptimalWarehouseAsync(tenantId, governorate, sku, quantity);
    return Results.Ok(allocation);
});

app.MapPost("/api/warehouses/transfers", async ([FromBody] CreateTransferDto req, IMultiWarehouseService whService) =>
{
    var transfer = await whService.CreateInterHubTransferAsync(req.TenantId, req.SourceWarehouseId, req.DestinationWarehouseId, req.Sku, req.Quantity);
    return Results.Ok(transfer);
});

app.MapPost("/api/warehouses/transfers/dispatch", async ([FromBody] ActionTransferDto req, IMultiWarehouseService whService) =>
{
    var transfer = await whService.DispatchInterHubTransferAsync(req.TenantId, req.TransferId);
    return Results.Ok(transfer);
});

app.MapPost("/api/warehouses/transfers/receive", async ([FromBody] ActionTransferDto req, IMultiWarehouseService whService) =>
{
    var transfer = await whService.ReceiveInterHubTransferAsync(req.TenantId, req.TransferId);
    return Results.Ok(transfer);
});

app.MapPost("/api/warehouses/evaluate-rop", async ([FromBody] EvaluateRopDto req, IMultiWarehouseService whService) =>
{
    var po = await whService.EvaluateReorderPointAndGeneratePoAsync(req.TenantId, req.WarehouseId, req.Sku, req.DailyDemand, req.LeadDays, req.UnitCost);
    return Results.Ok(po);
});

// --- Live Social Commerce Endpoints ---
app.MapPost("/api/channels/live/process-comment", async ([FromBody] ProcessLiveCommentDto req, ILiveCommerceOverlayService liveService) =>
{
    var result = await liveService.ProcessLiveCommentAsync(req.TenantId, req.SessionId, req.CommenterUsername, req.CommentText, req.CustomerPhone, req.Governorate);
    return Results.Ok(result);
});

app.MapGet("/api/channels/live/{sessionId}/overlay", async (Guid sessionId, [FromQuery] Guid tenantId, ILiveCommerceOverlayService liveService) =>
{
    var overlay = await liveService.GetLiveOverlayStockFeedAsync(tenantId, sessionId);
    return Results.Ok(overlay);
});

// --- Treasury Cash Float & Forecasting Endpoints ---
app.MapGet("/api/finance/treasury/float", async ([FromQuery] Guid tenantId, ITreasuryFloatService treasuryService) =>
{
    var valuation = await treasuryService.CalculateFloatValuationAsync(tenantId);
    return Results.Ok(valuation);
});

app.MapGet("/api/finance/treasury/forecast-14day", async ([FromQuery] Guid tenantId, ITreasuryFloatService treasuryService) =>
{
    var forecast = await treasuryService.Generate14DayCashFlowForecastAsync(tenantId, DateTime.UtcNow);
    return Results.Ok(forecast);
});

// --- Creator Affiliate Escrow Endpoints ---
app.MapGet("/api/marketing/creators", async ([FromQuery] Guid tenantId, ICreatorAffiliateService affiliateService) =>
{
    var creators = await affiliateService.ListCreatorsAsync(tenantId);
    return Results.Ok(creators);
});

app.MapPost("/api/marketing/creators", async ([FromBody] CreatorProfile profile, ICreatorAffiliateService affiliateService) =>
{
    var created = await affiliateService.RegisterCreatorAsync(profile.TenantId, profile);
    return Results.Ok(created);
});

app.MapGet("/api/marketing/creators/{creatorId}/summary", async (Guid creatorId, [FromQuery] Guid tenantId, ICreatorAffiliateService affiliateService) =>
{
    var summary = await affiliateService.GetCreatorSummaryAsync(tenantId, creatorId);
    return Results.Ok(summary);
});

app.MapPost("/api/marketing/creators/payouts/batch", async ([FromBody] TenantActionDto req, ICreatorAffiliateService affiliateService) =>
{
    var payouts = await affiliateService.GeneratePayoutBatchAsync(req.TenantId);
    return Results.Ok(payouts);
});

// --- Reverse Logistics & Drop-and-Swap Endpoints ---
app.MapGet("/api/commerce/exchanges", async ([FromQuery] Guid tenantId, [FromQuery] ExchangeStatus? status, IReverseExchangeService reverseService) =>
{
    var list = await reverseService.ListExchangeRequestsAsync(tenantId, status);
    return Results.Ok(list);
});

app.MapPost("/api/commerce/exchanges/request", async ([FromBody] RequestExchangeDto req, IReverseExchangeService reverseService) =>
{
    var result = await reverseService.RequestExchangeAsync(req.TenantId, req.OrderId, req.OriginalSku, req.ReplacementSku, req.ReplacementDesc, req.Reason, req.CashAdjustment);
    return Results.Ok(result);
});

app.MapPost("/api/commerce/exchanges/manifest", async ([FromBody] DispatchSwapManifestDto req, IReverseExchangeService reverseService) =>
{
    var manifest = await reverseService.ApproveAndDispatchSwapManifestAsync(req.TenantId, req.ExchangeRequestId, req.CourierProviderId, req.CourierName, req.SwapDeliveryFee);
    return Results.Ok(manifest);
});

app.MapPost("/api/commerce/exchanges/doorstep-confirm", async ([FromBody] ConfirmDoorstepSwapDto req, IReverseExchangeService reverseService) =>
{
    var result = await reverseService.ConfirmDoorstepSwapAsync(req.TenantId, req.ManifestId, req.HandoverNewParcel, req.ReturnParcelCollected);
    return Results.Ok(result);
});

app.MapPost("/api/commerce/exchanges/intake-inspect", async ([FromBody] ReturnIntakeInspectDto req, IReverseExchangeService reverseService) =>
{
    var inspection = await reverseService.ProcessWarehouseReturnIntakeAsync(req.TenantId, req.ExchangeRequestId, req.Grade, req.Notes);
    return Results.Ok(inspection);
});

// --- Cross-Border Trade & Customs Invoices Endpoints ---
app.MapGet("/api/commerce/cross-border/countries", async ([FromQuery] Guid tenantId, ICrossBorderTradeService tradeService) =>
{
    var countries = await tradeService.ListSupportedCountriesAsync(tenantId);
    return Results.Ok(countries);
});

app.MapPost("/api/commerce/cross-border/countries", async ([FromBody] CountryTradeConfig config, ICrossBorderTradeService tradeService) =>
{
    var result = await tradeService.ConfigureCountryAsync(config.TenantId, config);
    return Results.Ok(result);
});

app.MapGet("/api/commerce/cross-border/invoices", async ([FromQuery] Guid tenantId, ICrossBorderTradeService tradeService) =>
{
    var invoices = await tradeService.ListCommercialInvoicesAsync(tenantId);
    return Results.Ok(invoices);
});

// --- Demand Forecasting & Restock Endpoints ---
app.MapGet("/api/commerce/forecasting/alerts", async ([FromQuery] Guid tenantId, [FromQuery] RestockPriority? minPriority, IDemandForecastingService forecastService) =>
{
    var alerts = await forecastService.ListStockoutAlertsAsync(tenantId, minPriority);
    return Results.Ok(alerts);
});

app.MapGet("/api/commerce/forecasting/suppliers", async ([FromQuery] Guid tenantId, IDemandForecastingService forecastService) =>
{
    var suppliers = await forecastService.ListSuppliersAsync(tenantId);
    return Results.Ok(suppliers);
});

app.MapGet("/api/commerce/forecasting/restock-orders", async ([FromQuery] Guid tenantId, IDemandForecastingService forecastService) =>
{
    var orders = await forecastService.ListRestockOrdersAsync(tenantId);
    return Results.Ok(orders);
});

app.MapPost("/api/commerce/forecasting/restock-orders", async ([FromBody] GenerateRestockOrderDto req, IDemandForecastingService forecastService) =>
{
    var order = await forecastService.GenerateAutomatedRestockOrderAsync(req.TenantId, req.AlertId, req.SupplierId, req.UnitCost);
    return Results.Ok(order);
});

// --- RBAC & Security Audit Log Endpoints ---
app.MapGet("/api/security/audit-logs", async ([FromQuery] Guid tenantId, [FromQuery] string? resource, ISecurityAuditAndRbacService secService) =>
{
    var logs = await secService.ListAuditLogsAsync(tenantId, resource);
    return Results.Ok(logs);
});

app.MapGet("/api/security/orders/{orderId}/masked", async (Guid orderId, [FromQuery] Guid tenantId, [FromQuery] UserSystemRole userRole, ISecurityAuditAndRbacService secService) =>
{
    var view = await secService.GetMaskedCustomerOrderAsync(tenantId, orderId, userRole);
    return Results.Ok(view);
});

// --- Closed-Loop Delivered ROAS & Ad Optimizer Endpoints ---
app.MapGet("/api/marketing/ad-optimizer/metrics", async ([FromQuery] Guid tenantId, [FromQuery] AdPlatformType? platform, IAdSpendOptimizerService optimizerService) =>
{
    var metrics = await optimizerService.ListRoasMetricsAsync(tenantId, platform);
    return Results.Ok(metrics);
});

app.MapPost("/api/marketing/ad-optimizer/ingest", async ([FromBody] CampaignAdSpendRecord record, IAdSpendOptimizerService optimizerService) =>
{
    var result = await optimizerService.IngestAdSpendAsync(record.TenantId, record);
    return Results.Ok(result);
});

app.MapPost("/api/marketing/ad-optimizer/calculate-roas", async ([FromBody] CalculateRoasDto req, IAdSpendOptimizerService optimizerService) =>
{
    var metric = await optimizerService.CalculateDeliveredRoasAsync(req.TenantId, req.Platform, req.AdSetId, req.AdSetName, req.Date, req.AdSpend, req.AttributedOrderIds);
    return Results.Ok(metric);
});

app.MapPost("/api/marketing/ad-optimizer/rules", async ([FromBody] AdOptimizationRule rule, IAdSpendOptimizerService optimizerService) =>
{
    var result = await optimizerService.ConfigureRuleAsync(rule.TenantId, rule);
    return Results.Ok(result);
});

app.MapPost("/api/marketing/ad-optimizer/evaluate/{metricId}", async (Guid metricId, [FromQuery] Guid tenantId, [FromQuery] decimal dailyBudget, IAdSpendOptimizerService optimizerService) =>
{
    var log = await optimizerService.EvaluateAndApplyRulesAsync(tenantId, metricId, dailyBudget);
    return Results.Ok(log);
});

app.MapGet("/api/marketing/ad-optimizer/logs", async ([FromQuery] Guid tenantId, IAdSpendOptimizerService optimizerService) =>
{
    var logs = await optimizerService.ListExecutionLogsAsync(tenantId);
    return Results.Ok(logs);
});

// --- In-House Fleet & Driver Endpoints ---
app.MapGet("/api/commerce/fleet/drivers", async ([FromQuery] Guid tenantId, IInHouseFleetService fleetService) =>
{
    var drivers = await fleetService.ListDriversAsync(tenantId);
    return Results.Ok(drivers);
});

app.MapPost("/api/commerce/fleet/drivers", async ([FromBody] DeliveryDriver driver, IInHouseFleetService fleetService) =>
{
    var result = await fleetService.RegisterDriverAsync(driver.TenantId, driver);
    return Results.Ok(result);
});

app.MapPost("/api/commerce/fleet/run-sheets", async ([FromBody] GenerateRunSheetDto req, IInHouseFleetService fleetService) =>
{
    var sheet = await fleetService.GenerateRunSheetAsync(req.TenantId, req.DriverId, req.HubWarehouseId, req.OrderIds);
    return Results.Ok(sheet);
});

app.MapPost("/api/commerce/fleet/stops/{stopId}/deliver", async (Guid stopId, [FromBody] DeliverStopDto req, IInHouseFleetService fleetService) =>
{
    var result = await fleetService.RecordStopDeliveryAsync(req.TenantId, stopId, req.Status, req.PaymentMethod, req.FailureReason, req.SignatureData, req.PhotoUrl, req.Lat, req.Lon);
    return Results.Ok(result);
});

app.MapPost("/api/commerce/fleet/run-sheets/{runSheetId}/reconcile", async (Guid runSheetId, [FromBody] ReconcileCashDto req, IInHouseFleetService fleetService) =>
{
    var recon = await fleetService.ReconcileDriverCashAsync(req.TenantId, runSheetId, req.ActualCashTurnedIn, req.ReconciledBy, req.Notes);
    return Results.Ok(recon);
});

// --- WhatsApp Flow Engine Endpoints ---
app.MapGet("/api/channels/flows", async ([FromQuery] Guid tenantId, IWhatsAppFlowEngineService flowService) =>
{
    var flows = await flowService.ListFlowsAsync(tenantId);
    return Results.Ok(flows);
});

app.MapPost("/api/channels/flows", async ([FromBody] FlowDefinition flow, IWhatsAppFlowEngineService flowService) =>
{
    var created = await flowService.CreateFlowDefinitionAsync(flow.TenantId, flow);
    return Results.Ok(created);
});

app.MapPost("/api/channels/flows/execute-turn", async ([FromBody] ExecuteFlowTurnDto req, IWhatsAppFlowEngineService flowService) =>
{
    var turn = await flowService.ExecuteFlowTurnAsync(req.TenantId, req.FlowId, req.CustomerPhone, req.Message);
    return Results.Ok(turn);
});

// --- Inventory Liquidation & Markdown Endpoints ---
app.MapGet("/api/commerce/liquidation/aging", async ([FromQuery] Guid tenantId, [FromQuery] StockAgeTier? tier, IInventoryLiquidationService liquidationService) =>
{
    var list = await liquidationService.ListAgingLedgerAsync(tenantId, tier);
    return Results.Ok(list);
});

app.MapPost("/api/commerce/liquidation/intake-aging", async ([FromBody] IntakeAgingDto req, IInventoryLiquidationService liquidationService) =>
{
    var ledger = await liquidationService.RecordStockIntakeAgingAsync(req.TenantId, req.ProductId, req.Sku, req.ProductName, req.WarehouseId, req.Quantity, req.CostPrice, req.RetailPrice, req.DaysInWarehouse);
    return Results.Ok(ledger);
});

app.MapPost("/api/commerce/liquidation/bundles", async ([FromBody] GenerateBundleDto req, IInventoryLiquidationService liquidationService) =>
{
    var bundle = await liquidationService.GenerateLiquidationBundleAsync(req.TenantId, req.WinningSku, req.AgingSku, req.BundleName, req.BundleDiscountPct);
    return Results.Ok(bundle);
});

app.MapGet("/api/commerce/liquidation/bundles", async ([FromQuery] Guid tenantId, IInventoryLiquidationService liquidationService) =>
{
    var bundles = await liquidationService.ListActiveBundleOffersAsync(tenantId);
    return Results.Ok(bundles);
});

// Real-Time SignalR Hub
// --- Health & Readiness Probes ---
app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));
app.MapGet("/readyz", () => Results.Ok(new { status = "Ready", timestamp = DateTime.UtcNow }));

// --- Courier Companion Mobile PWA Endpoints ---
app.MapPost("/api/commerce/courier/session", async ([FromBody] StartDriverSessionDto req, ICourierCompanionService courierService) =>
{
    var sess = await courierService.StartDriverSessionAsync(req.TenantId, req.DriverId, req.DriverName, req.DeviceModel ?? "Android PWA");
    return Results.Ok(sess);
});

app.MapGet("/api/commerce/courier/run-sheet", async ([FromQuery] Guid tenantId, [FromQuery] Guid driverId, ICourierCompanionService courierService) =>
{
    var sheet = await courierService.GetActiveRunSheetAsync(tenantId, driverId);
    return Results.Ok(sheet);
});

app.MapGet("/api/commerce/courier/run-sheets/{runSheetId}/stops", async (Guid runSheetId, [FromQuery] Guid tenantId, ICourierCompanionService courierService) =>
{
    var stops = await courierService.GetRunSheetStopsAsync(tenantId, runSheetId);
    return Results.Ok(stops);
});

app.MapPost("/api/commerce/courier/pod", async ([FromBody] SubmitPodDto req, ICourierCompanionService courierService) =>
{
    var pod = await courierService.SubmitDoorstepProofOfDeliveryAsync(
        req.TenantId, req.StopId, req.OrderId, req.OrderNumber, req.DriverId,
        req.SignatureBase64, req.PhotoUrl, req.CashCollected, req.RecipientName,
        req.RecipientRelation ?? "Self", req.Lat ?? 36.8065, req.Lng ?? 10.1815, req.PaymentMethod ?? "CashOnDelivery"
    );
    return Results.Ok(pod);
});

app.MapGet("/api/commerce/courier/cash-summary", async ([FromQuery] Guid tenantId, [FromQuery] Guid driverId, ICourierCompanionService courierService) =>
{
    var summary = await courierService.GetDriverCashSummaryAsync(tenantId, driverId);
    return Results.Ok(summary);
});

app.MapPost("/api/commerce/courier/gps-heartbeat", async ([FromBody] DriverGpsHeartbeatDto req, ICourierCompanionService courierService) =>
{
    var ok = await courierService.PingDriverGpsHeartbeatAsync(req.TenantId, req.DriverId, req.Latitude, req.Longitude, req.BatteryPercent);
    return Results.Ok(new { success = ok });
});

// --- Headless 1-Page COD Checkout Endpoints ---
app.MapPost("/api/commerce/checkout/session", async ([FromBody] CreateCheckoutSessionDto req, ICodCheckoutService checkoutService) =>
{
    var sess = await checkoutService.CreateSessionAsync(req.TenantId, req.CartItemsJson, req.Subtotal);
    return Results.Ok(sess);
});

app.MapGet("/api/commerce/checkout/wilayas", async ([FromQuery] string countryCode, ICodCheckoutService checkoutService) =>
{
    var wilayas = await checkoutService.GetWilayasAsync(string.IsNullOrWhiteSpace(countryCode) ? "TN" : countryCode);
    return Results.Ok(wilayas);
});

app.MapPost("/api/commerce/checkout/details", async ([FromBody] UpdateCheckoutDetailsDto req, ICodCheckoutService checkoutService) =>
{
    var sess = await checkoutService.UpdateCustomerDetailsAsync(req.TenantId, req.SessionKey, req.CustomerName, req.CustomerPhone, req.WilayaCode, req.DelegationName, req.StreetAddress);
    return Results.Ok(sess);
});

app.MapPost("/api/commerce/checkout/send-otp", async ([FromBody] SendOtpDto req, ICodCheckoutService checkoutService) =>
{
    var code = await checkoutService.SendPhoneOtpAsync(req.TenantId, req.SessionKey);
    return Results.Ok(new { success = true, simulatedOtp = code });
});

app.MapPost("/api/commerce/checkout/verify-otp", async ([FromBody] VerifyOtpDto req, ICodCheckoutService checkoutService) =>
{
    var ok = await checkoutService.VerifyPhoneOtpAsync(req.TenantId, req.SessionKey, req.OtpInput);
    return Results.Ok(new { verified = ok });
});

app.MapPost("/api/commerce/checkout/apply-upsell", async ([FromBody] ApplyUpsellDto req, ICodCheckoutService checkoutService) =>
{
    var sess = await checkoutService.ApplyUpsellOfferAsync(req.TenantId, req.SessionKey, req.UpsellSku, req.UpsellAmount);
    return Results.Ok(sess);
});

app.MapPost("/api/commerce/checkout/complete", async ([FromBody] CompleteCheckoutDto req, ICodCheckoutService checkoutService) =>
{
    var confirmation = await checkoutService.CompleteCodOrderAsync(req.TenantId, req.SessionKey);
    return Results.Ok(confirmation);
});

// --- Autonomous UGC Creative Script Engine Endpoints ---
app.MapPost("/api/marketing/ugc/generate-scripts", async ([FromBody] GenerateUgcScriptsDto req, IUgcCreativeGeneratorService ugcService) =>
{
    var scripts = await ugcService.GenerateScriptsForProductAsync(req.TenantId, req.Sku, req.ProductName, req.Language ?? "Darija", req.TargetAudience ?? "Shoppers 18-35");
    return Results.Ok(scripts);
});

app.MapGet("/api/marketing/ugc/scripts", async ([FromQuery] Guid tenantId, [FromQuery] string sku, IUgcCreativeGeneratorService ugcService) =>
{
    var scripts = await ugcService.GetScriptsByProductAsync(tenantId, sku);
    return Results.Ok(scripts);
});

// --- Competitor Ad & Price Intelligence Radar Endpoints ---
app.MapPost("/api/marketing/competitor/ad", async ([FromBody] IngestCompetitorAdDto req, ICompetitorIntelligenceService compService) =>
{
    var ad = await compService.IngestCompetitorAdAsync(req.TenantId, req.CompetitorName, req.Platform, req.HookSnippet, req.OfferDetails, req.EstimatedDailySpend, req.ThreatLevel ?? "Medium");
    return Results.Ok(ad);
});

app.MapPost("/api/marketing/competitor/price", async ([FromBody] TrackCompetitorPriceDto req, ICompetitorIntelligenceService compService) =>
{
    var tracker = await compService.TrackPriceDisparityAsync(req.TenantId, req.Sku, req.CompetitorName, req.Url, req.CompetitorPrice, req.OurPrice);
    return Results.Ok(tracker);
});

app.MapGet("/api/marketing/competitor/radar", async ([FromQuery] Guid tenantId, ICompetitorIntelligenceService compService) =>
{
    var summary = await compService.GetRadarSummaryAsync(tenantId);
    return Results.Ok(summary);
});

// --- Enterprise B2B Financials & Factoring Endpoints ---
app.MapPost("/api/billing/b2b-credit/accounts", async ([FromBody] EstablishCreditDto req, IB2BCreditAndFactoringService b2bService) =>
{
    var account = await b2bService.EstablishCreditAccountAsync(req.TenantId, req.CompanyId, req.CompanyName, req.CreditLimit, req.PaymentTermsDays, req.CreditScoreRating ?? "AA");
    return Results.Ok(account);
});

app.MapPost("/api/billing/b2b-credit/invoice-drawdown", async ([FromBody] InvoiceDrawdownDto req, IB2BCreditAndFactoringService b2bService) =>
{
    var account = await b2bService.RecordInvoiceDrawdownAsync(req.TenantId, req.CompanyId, req.InvoiceAmount);
    return Results.Ok(account);
});

app.MapPost("/api/billing/b2b-credit/settlement", async ([FromBody] PaymentSettlementDto req, IB2BCreditAndFactoringService b2bService) =>
{
    var account = await b2bService.RecordPaymentSettlementAsync(req.TenantId, req.CompanyId, req.PaymentAmount);
    return Results.Ok(account);
});

app.MapPost("/api/billing/factoring/request", async ([FromBody] RequestFactoringDto req, IB2BCreditAndFactoringService b2bService) =>
{
    var record = await b2bService.RequestCourierCashFactoringAsync(req.TenantId, req.CourierId, req.CourierName, req.InTransitFloat, req.DeliveredFloat, req.DiscountRate);
    return Results.Ok(record);
});

app.MapPost("/api/billing/factoring/fund/{factoringId}", async (Guid factoringId, [FromQuery] Guid tenantId, IB2BCreditAndFactoringService b2bService) =>
{
    var funded = await b2bService.FundFactoringAdvanceAsync(tenantId, factoringId);
    return Results.Ok(funded);
});

app.MapGet("/api/billing/b2b-financials/summary", async ([FromQuery] Guid tenantId, IB2BCreditAndFactoringService b2bService) =>
{
    var summary = await b2bService.GetFinancialSummaryAsync(tenantId);
    return Results.Ok(summary);
});

// --- Public Developer Gateway Endpoints ---
app.MapPost("/api/developer/api-keys", async ([FromBody] CreateApiKeyDto req, IDeveloperGatewayService devService) =>
{
    var (apiKey, plainKey) = await devService.CreateApiKeyAsync(req.TenantId, req.Name, req.Scopes, req.IsLive, req.RateLimit);
    return Results.Ok(new { apiKey, secretKey = plainKey });
});

app.MapPost("/api/developer/api-keys/validate", async ([FromBody] ValidateKeyDto req, IDeveloperGatewayService devService) =>
{
    var res = await devService.ValidateApiKeyAsync(req.SecretKey);
    return Results.Ok(res);
});

app.MapPost("/api/developer/webhooks", async ([FromBody] RegisterWebhookDto req, IDeveloperGatewayService devService) =>
{
    var sub = await devService.RegisterWebhookAsync(req.TenantId, req.TargetUrl, req.Events);
    return Results.Ok(sub);
});

app.MapPost("/api/developer/webhooks/dispatch", async ([FromBody] DispatchWebhookDto req, IDeveloperGatewayService devService) =>
{
    var logs = await devService.DispatchWebhookEventAsync(req.TenantId, req.EventType, req.Payload);
    return Results.Ok(logs);
});

app.MapPost("/api/developer/webhooks/replay/{attemptId}", async (Guid attemptId, [FromQuery] Guid tenantId, IDeveloperGatewayService devService) =>
{
    var log = await devService.ReplayWebhookAttemptAsync(tenantId, attemptId);
    return Results.Ok(log);
});

app.MapHub<OmniHub>("/hubs/omni");

// --- Track 1: Database Seeding Endpoints ---
app.MapPost("/api/dev/seed", async ([FromQuery] Guid? tenantId, DatabaseSeeder seeder) =>
{
    var seededTenantId = await seeder.SeedEnterpriseDemoAsync(tenantId);
    return Results.Ok(new { status = "Success", tenantId = seededTenantId });
});

// --- Track 2: PDF Document & Invoice Generation Endpoints ---
app.MapGet("/api/documents/{documentId}/certificate/download", async (Guid documentId, [FromQuery] Guid tenantId, SGrowingDbContext db, IDocumentPdfGeneratorService pdfService) =>
{
    var envelope = await db.DocumentEnvelopes.Include(e => e.Signers).FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == documentId);
    if (envelope == null) return Results.NotFound("Document envelope not found.");

    var signer = envelope.Signers.FirstOrDefault() ?? await db.EnvelopeSigners.FirstOrDefaultAsync(s => s.EnvelopeId == envelope.Id && s.TenantId == tenantId);
    string signerName = signer?.Name ?? "Signer";
    string signerEmail = signer?.Email ?? "signer@example.com";
    string signerIp = !string.IsNullOrEmpty(signer?.IpAddress) ? signer.IpAddress : "196.200.14.88";
    DateTime signedDate = signer?.SignedAtUtc ?? envelope.CompletedAtUtc ?? DateTime.UtcNow;
    string docHash = !string.IsNullOrEmpty(envelope.ChecksumSha256) ? envelope.ChecksumSha256 : "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    string certHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(docHash + signerName + signedDate.ToString("O"))));

    var auditTrail = new List<AuditTrailEventDto>
    {
        new("Envelope Created", envelope.CreatedAtUtc, "Platform Admin", "127.0.0.1"),
        new("Invitation Sent", envelope.CreatedAtUtc.AddMinutes(2), "Email Delivery Engine", "10.0.0.1"),
        new("Document Viewed", signedDate.AddMinutes(-5), signerEmail, signerIp),
        new("Cryptographically Signed", signedDate, signerEmail, signerIp)
    };

    var pdfBytes = pdfService.GenerateCertificatePdf(
        envelope.Id,
        envelope.Title,
        signerName,
        signerEmail,
        signerIp,
        signedDate,
        docHash,
        certHash,
        auditTrail
    );

    return Results.File(pdfBytes, "application/pdf", $"certificate_{envelope.Id:N}.pdf");
});

app.MapGet("/api/finance/invoices/{invoiceId}/download", async (Guid invoiceId, [FromQuery] Guid tenantId, SGrowingDbContext db, IDocumentPdfGeneratorService pdfService) =>
{
    var order = await db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == invoiceId);
    string orderNum = order?.OrderNumber ?? $"INV-{invoiceId:N}".Substring(0, 12).ToUpper();
    string customer = order?.CustomerName ?? "Enterprise Client";
    decimal total = order?.TotalAmount ?? 149.99m;
    decimal subtotal = Math.Round(total / 1.2m, 2);
    decimal tax = total - subtotal;
    string currency = order?.CodCurrency ?? "USD";

    var items = new List<InvoiceLineItemDto>
    {
        new("Enterprise SaaS & Commerce Platform Services", 1, subtotal, subtotal)
    };

    var pdfBytes = pdfService.GenerateInvoicePdf(
        invoiceId,
        orderNum,
        "Inspire Global Commerce & SaaS",
        customer,
        subtotal,
        tax,
        total,
        currency,
        items
    );

    return Results.File(pdfBytes, "application/pdf", $"invoice_{orderNum}.pdf");
});

// --- Provider Gateways (WhatsApp & Courier Logistics) ---
app.MapGet("/api/gateways/whatsapp/sessions/{instanceName}", async (string instanceName, IWhatsAppGatewayClient waClient) =>
{
    var state = await waClient.GetSessionStateAsync(instanceName);
    return Results.Ok(state);
});

app.MapPost("/api/gateways/whatsapp/messages/send", async ([FromBody] SendWhatsAppMessageDto req, IWhatsAppGatewayClient waClient) =>
{
    var res = await waClient.SendTextMessageAsync(req.InstanceName, req.RecipientPhone, req.Message);
    return Results.Ok(res);
});

app.MapPost("/api/gateways/whatsapp/webhook", async ([FromBody] JsonElement payload, IWhatsAppGatewayClient waClient) =>
{
    var parsed = await waClient.ParseWebhookPayloadAsync(payload);
    return Results.Ok(parsed);
});

app.MapPost("/api/gateways/couriers/labels/generate", async ([FromBody] GenerateCourierLabelDto req, ICourierLogisticsGateway courierGateway) =>
{
    var label = await courierGateway.GenerateShipmentLabelAsync(req.CourierProviderName, req.Details);
    return Results.Ok(label);
});

app.MapPost("/api/gateways/couriers/webhook", async ([FromBody] IngestCourierWebhookGatewayDto req, ICourierLogisticsGateway courierGateway) =>
{
    var normalized = await courierGateway.TranslateWebhookEventAsync(req.CourierProviderName, req.Payload);
    return Results.Ok(normalized);
});

// --- Track 3: Vector RAG & Semantic Knowledge Search ---
app.MapPost("/api/knowledge/semantic-search", async ([FromBody] SemanticSearchRequestDto req, ISemanticVectorRagService ragService) =>
{
    var results = await ragService.SearchKnowledgeAsync(req.TenantId, req.Query, req.TopK, req.MinScore);
    return Results.Ok(results);
});

app.MapPost("/api/knowledge/index-articles", async ([FromBody] IndexKnowledgeRequestDto req, ISemanticVectorRagService ragService) =>
{
    var indexedCount = await ragService.IndexTenantKnowledgeAsync(req.TenantId);
    return Results.Ok(new { status = "indexed", count = indexedCount });
});

// --- Phases 1-4 Core Inspiration Repositories Endpoint Mappings ---
app.MapChatbotFlowEndpoints();
app.MapInAppEngagementEndpoints();
app.MapInAppTourEndpoints();
app.MapCollisionAndSlaEndpoints();
app.MapUnifiedDeskEndpoints();
app.MapWarmupNetworkEndpoints();
app.MapAutonomousSdrFleetEndpoints();
app.MapSocialPublishingEndpoints();
app.MapSocialCommentToDmEndpoints();
app.MapMcpServerEndpoints();
app.MapFrappeCrmEnhancementsEndpoints();
app.MapHelpdeskCommercialContextEndpoints();
app.MapCpqQuoteAndEavEndpoints();
app.MapCryptographicSealingEndpoints();
app.MapSegmentTelemetryEndpoints();

// Frontiers 5-8 Endpoint Mappings
app.MapProposalPortalEndpoints();
app.MapVideoMeetingEndpoints();
app.MapMultiInboxRotationEndpoints();
app.MapCarrierDispatchEndpoints();

// Horizons 10-13 Endpoint Mappings
app.MapWhatsAppPairingEndpoints();
app.MapAutonomousFunnelEndpoints();
app.MapAgentFleetEndpoints();
app.MapTechnicalSeoCrawlerEndpoints();

// Horizon 16: Autonomous P2P Mailbox Warmup Network & ESP Spam Rescue Daemon
app.MapWarmupRescueEndpoints();

// Horizon 19: Customer 360 Unified Identity Graph & Multi-Touch Attribution Engine
app.MapCustomer360Endpoints();

// Horizon 21: B2B Wholesale Portal, Corporate Sub-Accounts & Credit Limit Ledger
app.MapB2BWholesaleEndpoints();

// Horizon 18: Configure, Price, Quote (CPQ) & Margin-Guarded Quotation Engine
app.MapCpqQuotationEndpoints();

// Horizon 22: Self-Healing Outbound Webhook Mesh & Circuit Breaker Engine
app.MapWebhookMeshEndpoints();

// Horizon 23: AI Predictive Sales Cadence & Optimal Contact Time Intelligence
app.MapPredictiveCadenceEndpoints();

// Horizon 24: Autonomous Inventory Replenishment & Vendor Purchase Order Engine
app.MapInventoryReplenishmentEndpoints();

// Horizon 25: Multi-Tenant Whitelabel Branding & Custom Domain SSL Provisioning Engine
app.MapWhitelabelBrandingEndpoints();

// Horizon 26: Autonomous Cash Flow Forecasting & Working Capital Float Engine
app.MapCashflowForecastingEndpoints();

// Horizon 27: Distributed Rate Limiting & Sliding Window Token Bucket Guard
app.MapRateLimiterEndpoints();

// Horizon 28: Predictive Customer Churn & Automated Retention Winback Daemon
app.MapRetentionEndpoints();

// Horizon 29: Tamper-Evident Immutable Audit Ledger with Merkle Tree Cryptographic Sealing
app.MapMerkleAuditEndpoints();

// Horizon 30: Multi-Channel Public Feedback & Feature Voting Board Engine
app.MapFeedbackBoardEndpoints();

// Horizon 31: Dynamic Multi-Currency Invoicing, Cross-Border VAT & Credit Note Ledger
app.MapTaxInvoiceEndpoints();

// Horizon 32: Hybrid Vector RAG Knowledge Base & Automated Ticket Autopilot
app.MapHybridRagEndpoints();

// Horizon 33: Document Page-by-Page Reading Telemetry & Heatmap Analytics Engine
app.MapDocumentTelemetryEndpoints();

if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedEnterpriseDemoAsync();
    Console.WriteLine("Demo database successfully seeded.");
}

app.Run();

public record IngestEventDto(Guid ContactId, string EventName, string? PayloadJson);
public record SubmitFeedbackDto(Guid SurveyFormId, Guid ContactId, int Score, string? FeedbackText);
public record KbQueryDto(string Question);
public record ResolveContactDto(string? Email, string? Phone, string? FirstName, string? LastName);
public record BookSlotDto(Guid SlotId, Guid ContactId, DateTime ScheduledTimeUtc);
public record CreateCheckoutDto(Guid ContactId, Guid ConversationId, string Title, decimal Amount, string? Currency = "USD");
public record AutonomousAgentQueryDto(Guid ConversationId, Guid ContactId, string Message);
public record VoiceTurnDto(Guid SessionId, string CallerSpeechTranscript);
public record CreateSocialPostDto(string Content, List<SocialPlatform> Platforms, DateTime? ScheduledAtUtc = null, List<string>? MediaUrls = null);
public record DeflectCommentDto(SocialPlatform Platform, string PostExternalId, string CommenterUsername, string CommentText);
public record CreateCompanyDto(string Name, string Domain, string? Industry = null, decimal Revenue = 0, Guid? ParentCompanyId = null);
public record AssociateCompanyDto(Guid ContactId, string ContactEmail);
public record TriggerWorkflowDto(string InputPayloadJson);

public record StreamAudioChunkDto(Guid TenantId, Guid SessionId, string CallSid, string StreamSid, string PayloadBase64, long SequenceNumber);
public record WarmTransferDto(Guid TenantId, Guid SessionId, string TargetSipUri, string Reason, string Summary);
public record IndexDocumentDto(Guid TenantId, Guid DocumentId, string Title, string Content, string? SourceUrl = null);
public record SearchRagDto(Guid TenantId, string Query, int TopK = 3);
public record EnrichLeadDto(Guid TenantId, string DomainOrEmail);
public record CreateContractDto(Guid TenantId, string Title, string ContractType, string SignerName, string SignerEmail, decimal ContractValue, Guid? CompanyId = null, Guid? DealId = null);
public record SignContractDto(string SignerSignatureName, string? SignedPdfUrl = null);

public record CreateProductDto(Guid TenantId, string Name, string Slug, string Description, ProductType ProductType, decimal BasePrice, string? Currency = "USD");
public record CreateVariantDto(Guid TenantId, string Sku, string Name, decimal Price, int InventoryStock = 100, string? AttributesJson = "{}");
public record CreateQuoteDto(Guid TenantId, Guid ContactId, List<CreateQuoteLineItemRequest> Items, Guid? CompanyId = null, Guid? DealId = null, decimal DiscountPercent = 0, decimal TaxPercent = 0, string? Currency = "USD", string? Notes = null);
public record CreateWebhookSubscriptionDto(Guid TenantId, string TargetUrl, string SecretToken, string? SubscribedEvents = "*");
public record PrivacyErasureDto(Guid TenantId, string ContactEmail);

public record RecordTouchpointDto(Guid TenantId, Guid ContactId, string Channel, string CampaignName, TouchpointStage Stage, Guid? DealId = null, decimal Cost = 0, string? UtmSource = null, string? UtmMedium = null, string? UtmCampaign = null);
public record CalculateAttributionDto(Guid TenantId, Guid DealId, AttributionModelType Model = AttributionModelType.WShaped);
public record RecordUsageDto(Guid TenantId, UsageMetricType Metric, long Quantity, string? Metadata = null);
public record CheckEntitlementDto(Guid TenantId, UsageMetricType Metric, long IncomingQuantity);
public record EvaluateQuietHoursDto(Guid TenantId, DateTime RecipientLocalTime, bool IsUrgent = false);
public record InspectPromptDto(string UserPrompt);
public record RouteModelDto(AiTaskComplexity Complexity, string? TaskHint = null);
public record RegisterToolDto(Guid TenantId, string Name, string Description, string SchemaJson, string TargetUrl, string? HttpMethod = "POST");
public record ExecuteToolDto(Guid TenantId, string ToolName, string ArgumentsJson);

// Omnichannel COD, Logistics, WMS, Cadences, CRM 360 & Portal DTOs
public record CreateCodOrderDto(Guid TenantId, string OrderNumber, string CustomerName, string PhoneNumber, string ShippingAddress, string GovernorateOrWilaya, string DelegationOrCommune, decimal TotalAmount, string? CountryCode = "TN", string? CodCurrency = "TND", decimal ShippingFee = 7.0m, string? AlternatePhone = null, string? ItemsJson = "[]", string? Notes = null);
public record AcquireLockDto(Guid TenantId, Guid OrderId, string AgentId, string AgentName, int LeaseMinutes = 3);
public record ReleaseLockDto(Guid TenantId, Guid OrderId, string AgentId);
public record RecordOutcomeDto(Guid TenantId, Guid OrderId, string AgentId, string AgentName, CallOutcome Outcome, string? Notes = null, DateTime? ScheduledCallbackUtc = null);
public record SendWaInteractiveDto(Guid TenantId, Guid OrderId);
public record HandleWaResponseDto(Guid TenantId, Guid OrderId, string Action);

public record DispatchShipmentDto(Guid TenantId, Guid OrderId, Guid CourierProviderId);
public record GenerateManifestDto(Guid TenantId, Guid CourierProviderId, List<Guid> OrderIds);
public record IngestCourierWebhookDto(Guid TenantId, string TrackingNumber, CourierShipmentStatus Status, decimal? CashRemitted = null, string? Notes = null);

public record WmsStockActionDto(Guid TenantId, Guid WarehouseId, string Sku, int Quantity, Guid OrderId, string? PerformedBy = null);
public record WmsQuarantineRestockDto(Guid TenantId, Guid WarehouseId, string Sku, int Quantity, bool IsDamaged, string? InspectorId = null);

public record CreateCadenceDto(Guid TenantId, string Name, string Description, string TargetAudience);
public record AddCadenceStepDto(int StepNumber, int DayOffset, CadenceChannel Channel, string? SubjectTemplate, string BodyTemplate);
public record EnrollCadenceLeadDto(Guid TenantId, Guid CadenceId, string LeadName, string Email, string PhoneNumber, string? Company = null);
public record ProcessDueStepsDto(Guid TenantId);
public record CadenceReplyStopDto(Guid TenantId, string EmailOrPhone, string Channel, string ReplyText);
public record CompleteTaskDto(Guid TenantId, string Outcome);

public record ResolveCustomerDto(Guid TenantId, string Name, string Phone, string? Email = null, CustomerChannelIdentityType? ChannelType = null, string? ChannelValue = null);
public record MergeCustomersDto(Guid TenantId, Guid PrimaryCustomerId, Guid DuplicateCustomerId, string? PerformedBy = null);

public record PublicTrackingDto(string OrderNumber, string Phone);
public record SubmitRmaDto(Guid TenantId, string OrderNumber, string Phone, RmaReason Reason, string? Notes = null, List<string>? PhotoUrls = null);
public record ReviewRmaDto(Guid TenantId, Guid RmaId, bool IsApproved, string ResolutionNotes, bool DispatchReverseCourier = false, Guid? CourierId = null);

// Conversion Mesh, Courier Clearing, In-Chat Commerce & Storefront DTOs
public record TrackConversionDto(Guid TenantId, AdPlatform Platform, ConversionEventType EventType, decimal ValueAmount, string? Currency = "USD", string? Email = null, string? Phone = null, string? FirstName = null, string? LastName = null, string? City = null, string? Country = null, string? Fbc = null, string? Fbp = null, string? UserAgent = null, string? IpAddress = null, string? CustomDataJson = null, string? EventId = null);
public record ReconcileRemittanceDto(Guid TenantId, Guid CourierProviderId, string StatementReference, List<ReportedRemittanceItemDto> Items, string? ReconciledBy = null);
public record ResolveDiscrepancyDto(Guid TenantId, Guid MatchId, string ResolutionNotes);
public record ParseChatCartDto(string MessageText, decimal DefaultUnitPrice = 65.0m, string? Currency = "TND");
public record CreateChatOrderDto(Guid TenantId, Guid ConversationId, InChatCartIntent Intent, Guid? WarehouseId = null);
public record PushFulfillmentDto(Guid TenantId, Guid OrderId, string CourierName, string TrackingNumber);
public record GenerateSheetsRowDto(Guid TenantId, Guid OrderId);

// Autonomous Fulfillment & Operations Expansion DTOs
public record QueueVoiceOrderDto(Guid TenantId, Guid OrderId, Guid? CampaignId = null);
public record ProcessVoiceTurnDto(Guid TenantId, Guid AttemptId, string? SpeechText, string? DtmfDigit);
public record CreatePickSlipDto(Guid TenantId, Guid OrderId, Guid WarehouseId);
public record ScanPickItemDto(Guid TenantId, Guid PickSlipId, string ScannedBarcodeOrSku);
public record CompletePackDto(Guid TenantId, Guid PickSlipId, string TrackingNumber, string CourierName);
public record ScanManifestParcelDto(Guid TenantId, Guid ManifestId, string TrackingNumber);
public record CloseManifestDto(Guid TenantId, Guid ManifestId, string DriverName, string DriverPhone, string DriverSignature);
public record InspectRmaParcelDto(Guid TenantId, string TrackingNumber, string InspectorId, RmaItemCondition Condition, string? Notes, Guid WarehouseId);
public record SyncMetaCatalogDto(Guid TenantId, string CatalogId);
public record WhatsAppInboundOrderWebhookDto(Guid TenantId, string CustomerPhone, string CustomerName, string CatalogId, List<WhatsAppInboundOrderItem> Items, string? ShippingAddress = null, string? ShippingGovernorate = null);
public record RegisterDomainDto(Guid TenantId, string Hostname, bool IsPrimary = true);
public record VerifyDomainDnsDto(Guid TenantId, Guid DomainId, bool SimulateValidDns = true);
public record UpsertBrandTokensDto(Guid TenantId, string StorefrontSlug, string BrandName, string PrimaryColorHex, string SecondaryColorHex, string FontFamily, string LogoUrl, string FaviconUrl, string SupportEmail, string SupportPhone, string SmsSenderId, string WhatsAppSenderPhone);
public record GenerateDepositLinkDto(Guid TenantId, Guid OrderId, string CustomerPhone, decimal DepositAmount = 10.0m, string Provider = "Flouci");
public record SettleDepositDto(Guid TenantId, Guid LinkId);

// Enterprise Operations Expansion DTOs
public record AutoAssignCarrierDto(Guid TenantId, Guid OrderId, decimal PackageWeightKg = 1.0m);
public record SlaBreachFallbackDto(Guid TenantId, string CurrentTrackingNumber);
public record ScheduleRetentionDto(Guid TenantId, Guid OrderId);
public record SubmitRetentionSurveyDto(Guid TenantId, Guid SurveyId, int Rating, string? Notes = null);
public record ProcessReplenishmentsDto(Guid TenantId, DateTime CurrentTimeUtc);
public record CreateTransferDto(Guid TenantId, Guid SourceWarehouseId, Guid DestinationWarehouseId, string Sku, int Quantity);
public record ActionTransferDto(Guid TenantId, Guid TransferId);
public record EvaluateRopDto(Guid TenantId, Guid WarehouseId, string Sku, int DailyDemand = 5, int LeadDays = 7, decimal UnitCost = 25.0m);
public record ProcessLiveCommentDto(Guid TenantId, Guid SessionId, string CommenterUsername, string CommentText, string? CustomerPhone = null, string? Governorate = null);

// Enterprise Scale Expansion DTOs
public record TenantActionDto(Guid TenantId);
public record RequestExchangeDto(Guid TenantId, Guid OrderId, string OriginalSku, string ReplacementSku, string ReplacementDesc, string Reason, decimal CashAdjustment = 0m);
public record DispatchSwapManifestDto(Guid TenantId, Guid ExchangeRequestId, Guid CourierProviderId, string CourierName, decimal SwapDeliveryFee = 9.5m);
public record ConfirmDoorstepSwapDto(Guid TenantId, Guid ManifestId, bool HandoverNewParcel = true, bool ReturnParcelCollected = true);
public record ReturnIntakeInspectDto(Guid TenantId, Guid ExchangeRequestId, SwapInspectionResult Grade, string Notes = "");
public record GenerateRestockOrderDto(Guid TenantId, Guid AlertId, Guid SupplierId, decimal UnitCost);

// Next-Gen Strategic Frontiers DTOs
public record CalculateRoasDto(Guid TenantId, AdPlatformType Platform, string AdSetId, string AdSetName, DateTime Date, decimal AdSpend, List<Guid> AttributedOrderIds);
public record GenerateRunSheetDto(Guid TenantId, Guid DriverId, Guid HubWarehouseId, List<Guid> OrderIds);
public record DeliverStopDto(Guid TenantId, StopDeliveryStatus Status, string PaymentMethod = "CashOnDelivery", string? FailureReason = null, string SignatureData = "", string PhotoUrl = "", double? Lat = null, double? Lon = null);
public record ReconcileCashDto(Guid TenantId, decimal ActualCashTurnedIn, string ReconciledBy, string Notes = "");
public record ExecuteFlowTurnDto(Guid TenantId, Guid FlowId, string CustomerPhone, string Message);
public record IntakeAgingDto(Guid TenantId, Guid ProductId, string Sku, string ProductName, Guid WarehouseId, int Quantity, decimal CostPrice, decimal RetailPrice, int DaysInWarehouse);
public record GenerateBundleDto(Guid TenantId, string WinningSku, string AgingSku, string BundleName, decimal BundleDiscountPct = 30m);


// Horizon 1-4 Strategic DTOs
public record StartDriverSessionDto(Guid TenantId, Guid DriverId, string DriverName, string? DeviceModel);
public record SubmitPodDto(Guid TenantId, Guid StopId, Guid OrderId, string OrderNumber, Guid DriverId, string SignatureBase64, string PhotoUrl, decimal CashCollected, string RecipientName, string? RecipientRelation, double? Lat, double? Lng, string? PaymentMethod);
public record DriverGpsHeartbeatDto(Guid TenantId, Guid DriverId, double Latitude, double Longitude, int BatteryPercent);
public record CreateCheckoutSessionDto(Guid TenantId, string CartItemsJson, decimal Subtotal);
public record UpdateCheckoutDetailsDto(Guid TenantId, string SessionKey, string CustomerName, string CustomerPhone, string WilayaCode, string DelegationName, string StreetAddress);
public record SendOtpDto(Guid TenantId, string SessionKey);
public record VerifyOtpDto(Guid TenantId, string SessionKey, string OtpInput);
public record ApplyUpsellDto(Guid TenantId, string SessionKey, string UpsellSku, decimal UpsellAmount);
public record CompleteCheckoutDto(Guid TenantId, string SessionKey);
public record GenerateUgcScriptsDto(Guid TenantId, string Sku, string ProductName, string? Language, string? TargetAudience);
public record IngestCompetitorAdDto(Guid TenantId, string CompetitorName, string Platform, string HookSnippet, string OfferDetails, decimal EstimatedDailySpend, string? ThreatLevel);
public record TrackCompetitorPriceDto(Guid TenantId, string Sku, string CompetitorName, string Url, decimal CompetitorPrice, decimal OurPrice);
public record EstablishCreditDto(Guid TenantId, Guid CompanyId, string CompanyName, decimal CreditLimit, int PaymentTermsDays, string? CreditScoreRating);
public record InvoiceDrawdownDto(Guid TenantId, Guid CompanyId, decimal InvoiceAmount);
public record PaymentSettlementDto(Guid TenantId, Guid CompanyId, decimal PaymentAmount);
public record RequestFactoringDto(Guid TenantId, string CourierId, string CourierName, decimal InTransitFloat, decimal DeliveredFloat, decimal DiscountRate = 2.5m);
public record CreateApiKeyDto(Guid TenantId, string Name, List<string> Scopes, bool IsLive = true, int RateLimit = 120);
public record ValidateKeyDto(string SecretKey);
public record RegisterWebhookDto(Guid TenantId, string TargetUrl, List<string> Events);
public record DispatchWebhookDto(Guid TenantId, string EventType, object Payload);

// Track 1-3 Extension DTOs
public record SendWhatsAppMessageDto(string InstanceName, string RecipientPhone, string Message);
public record GenerateCourierLabelDto(string CourierProviderName, CourierLabelRequestDto Details);
public record IngestCourierWebhookGatewayDto(string CourierProviderName, CourierTrackingWebhookPayloadDto Payload);
public record SemanticSearchRequestDto(Guid TenantId, string Query, int TopK = 5, double MinScore = 0.35);
public record IndexKnowledgeRequestDto(Guid TenantId);

// Required for WebApplicationFactory in integration tests
public partial class Program { }
