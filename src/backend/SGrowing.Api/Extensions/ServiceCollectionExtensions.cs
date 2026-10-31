using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Agents;
using SGrowing.Api.Modules.Agents.Git;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.AI.Personas;
using SGrowing.Api.Modules.Analytics;
using SGrowing.Api.Modules.Attribution;
using SGrowing.Api.Modules.Auth;
using SGrowing.Api.Modules.Automation;
using SGrowing.Api.Modules.Billing;
using SGrowing.Api.Modules.Billing.Invoicing;
using SGrowing.Api.Modules.Calendar;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Channels.Telegram;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Api.Modules.Channels.WhatsApp.Catalog;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Commerce.AntiFraud;
using SGrowing.Api.Modules.Commerce.CallCenter;
using SGrowing.Api.Modules.Commerce.CartReservations;
using SGrowing.Api.Modules.Commerce.Connectors;
using SGrowing.Api.Modules.Commerce.Shipping;
using SGrowing.Api.Modules.Commerce.Warehouses;
using SGrowing.Api.Modules.Contracts;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.Conversations.Macros;
using SGrowing.Api.Modules.Conversations.Mime;
using SGrowing.Api.Modules.Conversations.PresenceAndCsat;
using SGrowing.Api.Modules.Conversations.Sla;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.CRM.ContractSigning;
using SGrowing.Api.Modules.CRM.CustomFields;
using SGrowing.Api.Modules.CRM.Import;
using SGrowing.Api.Modules.Deliverability;
using SGrowing.Api.Modules.Dev;
using SGrowing.Api.Modules.Diagnostics;
using SGrowing.Api.Modules.Documents;
using SGrowing.Api.Modules.Feedback;
using SGrowing.Api.Modules.Feedback.Community;
using SGrowing.Api.Modules.Feedback.MicroSurveys;
using SGrowing.Api.Modules.Finance;
using SGrowing.Api.Modules.Funnels;
using SGrowing.Api.Modules.Funnels.Autonomous;
using SGrowing.Api.Modules.Gateways;
using SGrowing.Api.Modules.Gateways.Android;
using SGrowing.Api.Modules.Integrations;
using SGrowing.Api.Modules.Journeys;
using SGrowing.Api.Modules.KnowledgeBase;
using SGrowing.Api.Modules.KnowledgeBase.Autopilot;
using SGrowing.Api.Modules.Lifecycle;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Api.Modules.Organizations;
using SGrowing.Api.Modules.Outbound;
using SGrowing.Api.Modules.Outbound.EmailVerification;
using SGrowing.Api.Modules.Outbound.Enrichment;
using SGrowing.Api.Modules.Outbound.Research;
using SGrowing.Api.Modules.Outbound.Spintax;
using SGrowing.Api.Modules.Projects;
using SGrowing.Api.Modules.Sales;
using SGrowing.Api.Modules.Scheduling;
using SGrowing.Api.Modules.Security;
using SGrowing.Api.Modules.SEO;
using SGrowing.Api.Modules.Social;
using SGrowing.Api.Modules.Storage.Media;
using SGrowing.Api.Modules.Telemetry;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;
using SGrowing.Api.Modules.Voice;
using SGrowing.Api.Modules.Voice.Streaming;
using SGrowing.Api.Modules.Workforce.Timesheets;
using SGrowing.Api.Services;
using SGrowing.Core.Channels;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Events;
using SGrowing.Core.Messaging;
using SGrowing.Core.Outbox;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseAndTenancy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<SGrowingDbContext>((sp, options) =>
        {
            var connStr = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connStr))
            {
                options.UseNpgsql(connStr);
            }
            else
            {
                options.UseInMemoryDatabase("SGrowingDev");
            }
        });

        services.AddScoped<IPostgreSqlRlsService, PostgreSqlRlsService>();
        services.AddScoped<TenantRlsConnectionInterceptor>();

        return services;
    }

    public static IServiceCollection AddCoreAndMessagingServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Core & Outbox
        services.AddScoped<IOutbox, OutboxService>();
        services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();
        services.AddScoped<IAiCopilotService, AiCopilotService>();
        services.AddScoped<ChannelIngressService>();

        // Concurrency, Deduplication & Events
        services.AddSingleton<SGrowing.Core.Common.IConcurrencyLockService, SGrowing.Core.Common.DistributedMemoryLockService>();
        services.AddSingleton<IEventDeduplicationService, EventDeduplicationService>();
        services.AddSingleton<IEventStreamBroker, InMemoryEventStreamBroker>();
        services.AddScoped<IMultiBrokerEventFanoutService, MultiBrokerEventFanoutService>();

        // Channel Adapters
        services.AddSingleton<WhatsAppCloudAdapter>();
        services.AddSingleton<EvolutionApiAdapter>();
        services.AddSingleton<EmailAdapter>();
        services.AddSingleton<TelegramAdapter>();
        services.AddSingleton<DiscordAdapter>();
        services.AddSingleton<MetaGraphAdapter>();
        services.AddSingleton<XTwitterAdapter>();
        services.AddSingleton<SmsAdapter>();

        // Redis Stream Broker
        var redisConn = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            try
            {
                services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ => StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
                services.AddSingleton<IRedisStreamBroker, RedisStreamBroker>();
            }
            catch
            {
                services.AddSingleton<IRedisStreamBroker, InMemoryStreamBroker>();
            }
        }
        else
        {
            services.AddSingleton<IRedisStreamBroker, InMemoryStreamBroker>();
        }

        // Daemon Heartbeat & SignalR / CORS
        services.AddSingleton<IDaemonHeartbeatService, DaemonHeartbeatService>();
        services.AddSignalR();
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddOmnichannelAndCommerceServices(this IServiceCollection services)
    {
        // CRM Handlers
        services.AddScoped<CreateContactHandler>();
        services.AddScoped<GetContactHandler>();
        services.AddScoped<SearchContactsHandler>();
        services.AddScoped<SendMessageHandler>();
        services.AddScoped<ChangeStatusHandler>();
        services.AddScoped<GetConversationMessagesHandler>();
        services.AddScoped<ApproveAiDraftHandler>();

        // Autonomous AI Agent
        services.AddScoped<IAgentTool, KnowledgeBaseTool>();
        services.AddScoped<IAgentTool, OrderLookupTool>();
        services.AddScoped<IAgentTool, EscalateToHumanTool>();
        services.AddScoped<AutonomousAiAgentService>();
        services.AddScoped<IdentityResolverService>();
        services.AddScoped<BroadcastDispatcherService>();
        services.AddScoped<AppointmentBookingService>();
        services.AddScoped<CheckoutLinkService>();
        services.AddScoped<SlaMonitoringService>();

        // Voice & Telephony
        services.AddScoped<VoiceTelephonyService>();
        services.AddScoped<RealtimeVoiceAgentService>();
        services.AddScoped<VoiceAiStreamService>();
        services.AddSingleton<BargeInInterruptionService>();
        services.AddSingleton<StreamingVoiceWebSocketHandler>();
        services.AddSingleton<OpenAiRealtimeBridgeService>();
        services.AddSingleton<BrowserSoftphoneSignalingService>();
        services.AddScoped<IAutomatedVoiceDialerService, AutomatedVoiceDialerService>();
        services.AddScoped<IVoiceCallAmdService, VoiceCallAmdService>();
        services.AddSingleton<IVoiceTranscriptionDiarizationService, VoiceTranscriptionDiarizationService>();
        services.AddSingleton<IVoiceAiStreamingBridgeService, VoiceAiStreamingBridgeService>();
        services.AddSingleton<IAudioTranscriptionService, AudioTranscriptionService>();

        // Outbound SDR
        services.AddScoped<MailboxService>();
        services.AddSingleton<IAiSdrService, AiSdrService>();
        services.AddSingleton<ReplySentimentClassifier>();
        services.AddScoped<ProspectReplyHandoffService>();
        services.AddScoped<CadenceDispatcherService>();
        services.AddScoped<CreateCampaignHandler>();
        services.AddScoped<EnrollProspectsHandler>();
        services.AddScoped<IPowerDialerService, PowerDialerService>();
        services.AddScoped<LinkedInOutreachAdapter>();
        services.AddScoped<MultiTouchCadenceOrchestrator>();
        services.AddScoped<ISalesCadenceEngine, SalesCadenceEngine>();

        // Commerce, WMS & Logistics
        services.AddScoped<ProductCatalogService>();
        services.AddScoped<InChatCpqService>();
        services.AddScoped<StripePaymentWebhookHandler>();
        services.AddScoped<ShopifyWebhookReceiver>();
        services.AddScoped<AbandonedCartRecoveryService>();
        services.AddScoped<ICodConfirmationService, CodConfirmationService>();
        services.AddScoped<ICourierLogisticsHubService, CourierLogisticsHubService>();
        services.AddScoped<IDefensiveWmsService, DefensiveWmsService>();
        services.AddScoped<ICourierRemittanceClearingService, CourierRemittanceClearingService>();
        services.AddScoped<IConversationalCartEngine, ConversationalCartEngine>();
        services.AddScoped<IExternalStoreAndSheetsService, ExternalStoreAndSheetsService>();
        services.AddScoped<IBarcodeFulfillmentService, BarcodeFulfillmentService>();
        services.AddScoped<IWhatsAppCatalogEngine, WhatsAppCatalogEngine>();
        services.AddScoped<IStorefrontDomainService, StorefrontDomainService>();
        services.AddScoped<ICustomerReputationService, CustomerReputationService>();
        services.AddScoped<ISmartCourierRoutingService, SmartCourierRoutingService>();
        services.AddScoped<IMultiWarehouseService, MultiWarehouseService>();
        services.AddScoped<ILiveCommerceOverlayService, LiveCommerceOverlayService>();
        services.AddScoped<ITreasuryFloatService, TreasuryFloatService>();
        services.AddScoped<ICreatorAffiliateService, CreatorAffiliateService>();
        services.AddScoped<IReverseExchangeService, ReverseExchangeService>();
        services.AddScoped<ICrossBorderTradeService, CrossBorderTradeService>();
        services.AddScoped<IDemandForecastingService, DemandForecastingService>();
        services.AddScoped<IAdSpendOptimizerService, AdSpendOptimizerService>();
        services.AddScoped<IInHouseFleetService, InHouseFleetService>();
        services.AddScoped<IWhatsAppFlowEngineService, WhatsAppFlowEngineService>();
        services.AddScoped<IInventoryLiquidationService, InventoryLiquidationService>();
        services.AddScoped<ICourierCompanionService, CourierCompanionService>();
        services.AddScoped<ICodCheckoutService, CodCheckoutService>();
        services.AddScoped<IB2BCreditAndFactoringService, B2BCreditAndFactoringService>();
        services.AddScoped<ICallCenterAntiCollisionService, CallCenterAntiCollisionService>();
        services.AddScoped<ICodNetMarginLedgerService, CodNetMarginLedgerService>();
        services.AddScoped<ICodCarrierAutoDispatchService, CodCarrierAutoDispatchService>();
        services.AddScoped<ICartReservationService, CartReservationService>();

        // Customer 360, RMA & Self-Service Portal
        services.AddScoped<ICustomerIdentityGraphService, CustomerIdentityGraphService>();
        services.AddScoped<ICustomer360AttributionService, Customer360AttributionService>();
        services.AddScoped<ICustomerSelfServicePortalService, CustomerSelfServicePortalService>();

        return services;
    }

    public static IServiceCollection AddEnterpriseHorizonsServices(this IServiceCollection services)
    {
        // Journeys, Feedback & Knowledge Base
        services.AddScoped<JourneyExecutionService>();
        services.AddScoped<IStatefulJourneyEngineService, StatefulJourneyEngineService>();
        services.AddScoped<FeedbackService>();
        services.AddScoped<HelpCenterRagService>();
        services.AddScoped<ICommunityFeedbackService, CommunityFeedbackService>();
        services.AddScoped<IInAppMicroSurveyService, InAppMicroSurveyService>();

        // Social & Marketing
        services.AddSingleton<ISocialPostRepository, InMemorySocialPostRepository>();
        services.AddScoped<SGrowing.Api.Modules.Marketing.SocialPublishingService>();
        services.AddScoped<SocialCommentDeflectionService>();
        services.AddSingleton<ISocialCommentToDmService, SocialCommentToDmService>();
        services.AddScoped<IPostPurchaseRetentionService, PostPurchaseRetentionService>();
        services.AddScoped<IUgcCreativeGeneratorService, UgcCreativeGeneratorService>();
        services.AddScoped<ICompetitorIntelligenceService, CompetitorIntelligenceService>();
        services.AddSingleton<ISocialPublishingService, SGrowing.Api.Modules.Social.SocialPublishingService>();

        // Workflows & Automation
        services.AddScoped<CompanyAccountService>();
        services.AddScoped<WorkflowEngineService>();
        services.AddScoped<IWorkflowGraphExecutorService, WorkflowGraphExecutorService>();

        // Security & Encryption Vault
        services.AddSingleton<AesEncryptionService>();
        services.AddScoped<ITenantSecretsVaultService, TenantSecretsVaultService>();
        services.AddSingleton<DocumentChunkingService>();
        services.AddScoped<VectorRagRetrievalService>();
        services.AddScoped<LeadEnrichmentService>();
        services.AddScoped<ContractSigningService>();
        services.AddScoped<CustomerPrivacyService>();
        services.AddScoped<ISecurityAuditAndRbacService, SecurityAuditAndRbacService>();
        services.AddSingleton<ITokenRevocationService, TokenRevocationService>();

        // Attribution & SaaS Metering
        services.AddScoped<MultiTouchAttributionService>();
        services.AddScoped<TenantUsageMeteringService>();
        services.AddScoped<QuietHoursGuardService>();
        services.AddSingleton<PromptFirewallService>();
        services.AddScoped<DynamicAgentToolRegistry>();
        services.AddScoped<IServerConversionMeshService, ServerConversionMeshService>();
        services.AddScoped<ISaasBillingService, SaasBillingService>();
        services.AddScoped<IPlanQuotaEnforcementService, PlanQuotaEnforcementService>();

        // SEO & Funnels
        services.AddScoped<ISeoAuditService, SeoAuditService>();
        services.AddScoped<ISalesFunnelService, SalesFunnelService>();
        services.AddScoped<ISeoMultipageGraphService, SeoMultipageGraphService>();
        services.AddScoped<ISerpRankTrackerService, SerpRankTrackerService>();
        services.AddScoped<IAutonomousFunnelPipelineService, AutonomousFunnelPipelineService>();
        services.AddScoped<IFunnelSplitTestService, FunnelSplitTestService>();
        services.AddScoped<IAutonomousFunnelBuilderService, AutonomousFunnelBuilderService>();

        // Calendar, Telemetry & Partitions
        services.AddScoped<ICalendarSchedulingService, CalendarSchedulingService>();
        services.AddScoped<IEmailDeliverabilityService, EmailDeliverabilityService>();
        services.AddScoped<IProductTelemetryService, ProductTelemetryService>();
        services.AddScoped<IDatabasePartitioningService, DatabasePartitioningService>();
        services.AddScoped<IClickstreamBufferService, ClickstreamBufferService>();

        // Digital Goods, Dynamic Forms & Developer Webhooks
        services.AddScoped<IDigitalGoodsService, DigitalGoodsService>();
        services.AddScoped<IDynamicFormService, DynamicFormService>();
        services.AddScoped<IDeveloperWebhookService, DeveloperWebhookService>();
        services.AddScoped<IDeveloperGatewayService, DeveloperGatewayService>();
        services.AddScoped<IDocumentTrackingService, DocumentTrackingService>();
        services.AddScoped<IPublicationCmsService, PublicationCmsService>();
        services.AddScoped<ISalesForecastingService, SalesForecastingService>();
        services.AddScoped<IFeatureFeedbackService, FeatureFeedbackService>();

        // AI Agents, Missions & Conversational BI
        services.AddScoped<IAiAgentOrchestratorService, AiAgentOrchestratorService>();
        services.AddScoped<IAutonomousAgentMissionService, AutonomousAgentMissionService>();
        services.AddScoped<IConversationalBiService, ConversationalBiService>();
        services.AddScoped<IAgentPersonaService, AgentPersonaService>();
        services.AddScoped<IAiModelRouterService, AiModelRouterService>();
        services.AddScoped<AiModelRouterService>();

        // PDF Generation, Gateways & Semantic RAG
        services.AddSingleton<IDocumentPdfGeneratorService, DocumentPdfGeneratorService>();
        services.AddSingleton<IWhatsAppGatewayClient, EvolutionApiGatewayClient>();
        services.AddHttpClient<IWhatsAppCloudApiClient, WhatsAppCloudApiAdapter>();
        services.AddSingleton<ILiveKitMediaClient, LiveKitMediaServerAdapter>();
        services.AddSingleton<ISmtpSocketClient, SmtpSocketClientAdapter>();
        services.AddSingleton<ICourierLogisticsGateway, CourierLogisticsGateway>();
        services.AddScoped<ISemanticVectorRagService, SemanticVectorRagService>();

        // Inspiration Repository Modules
        services.AddScoped<IChatbotFlowEngineService, ChatbotFlowEngineService>();
        services.AddScoped<IInAppEngagementService, InAppEngagementService>();
        services.AddScoped<IInAppTourService, InAppTourService>();
        services.AddScoped<ICollisionDetectionAndSlaService, CollisionDetectionAndSlaService>();
        services.AddScoped<IOmnichannelUnifiedDeskService, OmnichannelUnifiedDeskService>();
        services.AddScoped<IWarmupPeerNetworkService, WarmupPeerNetworkService>();
        services.AddScoped<IAutonomousSdrFleetService, AutonomousSdrFleetService>();
        services.AddScoped<ISocialPublishingEngineService, SocialPublishingEngineService>();
        services.AddScoped<IMcpServerRegistryService, McpServerRegistryService>();
        services.AddScoped<IFrappeCrmEnhancementsService, FrappeCrmEnhancementsService>();
        services.AddScoped<IHelpdeskCommercialContextService, HelpdeskCommercialContextService>();
        services.AddScoped<ICpqQuoteAndEavService, CpqQuoteAndEavService>();
        services.AddScoped<IDocumentTelemetryService, DocumentTelemetryService>();
        services.AddScoped<ICryptographicPdfSealingService, CryptographicPdfSealingService>();
        services.AddScoped<ISegmentTelemetryAndJourneysService, SegmentTelemetryAndJourneysService>();

        // Deliverability & Spintax
        services.AddSingleton<ISpintaxService, SpintaxService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IContactEnrichmentPipeline, ContactEnrichmentPipeline>();
        services.AddSingleton<IWarmupThreadGeneratorService, WarmupThreadGeneratorService>();
        services.AddScoped<IPlacementTestService, PlacementTestService>();
        services.AddSingleton<IMimeEmailParserService, MimeEmailParserService>();
        services.AddSingleton<ICannedMacroService, CannedMacroService>();
        services.AddScoped<ISlaEscalationEngineService, SlaEscalationEngineService>();
        services.AddScoped<ICsvImportService, CsvImportService>();
        services.AddSingleton<IBotSimulatorService, BotSimulatorService>();
        services.AddSingleton<IMultiPartySigningService, MultiPartySigningService>();
        services.AddSingleton<ICarrierShippingGateway, CarrierShippingGateway>();
        services.AddScoped<ICodAntiFraudEngine, CodAntiFraudEngine>();
        services.AddScoped<IShopifyConnectorService, ShopifyConnectorService>();
        services.AddScoped<IWooCommerceConnectorService, WooCommerceConnectorService>();

        // Ever-Teams & Ever-Gauzy
        services.AddScoped<IProjectTaskService, ProjectTaskService>();
        services.AddScoped<IScraperVaultService, ScraperVaultService>();
        services.AddScoped<IDeepWebResearchService, DeepWebResearchService>();
        services.AddScoped<IWarehouseRoutingService, WarehouseRoutingService>();
        services.AddSingleton<IGeodeticFleetDispatchService, GeodeticFleetDispatchService>();
        services.AddScoped<IOrganizationHierarchyService, OrganizationHierarchyService>();
        services.AddScoped<ITimesheetService, TimesheetService>();
        services.AddScoped<IWhatsAppGatewayService, WhatsAppGatewayService>();
        services.AddScoped<IMediaVaultService, MediaVaultService>();
        services.AddScoped<IInboundMessageRouterService, InboundMessageRouterService>();
        services.AddScoped<IOperatorAssignmentService, OperatorAssignmentService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IGitHubAutomationService, GitHubAutomationService>();
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        services.AddScoped<IWhatsAppNewsletterService, WhatsAppNewsletterService>();
        services.AddSingleton<IWhatsAppBroadcastPacingService, WhatsAppBroadcastPacingService>();
        services.AddSingleton<IWhatsAppCatalogCartService, WhatsAppCatalogCartService>();
        services.AddSingleton<ITelegramMarketingBotService, TelegramMarketingBotService>();

        // Presence, Snapshots & Rebilling
        services.AddScoped<IConversationPresenceService, ConversationPresenceService>();
        services.AddScoped<ICsatSurveyService, CsatSurveyService>();
        services.AddScoped<ITenantSnapshotService, TenantSnapshotService>();
        services.AddScoped<ITenantRebillingService, TenantRebillingService>();
        services.AddScoped<IWaterfallEnrichmentService, WaterfallEnrichmentService>();
        services.AddScoped<IAndroidSmsGatewayService, AndroidSmsGatewayService>();

        // Frontiers 5-9 & Horizons 10-52
        services.AddScoped<IProposalPortalService, ProposalPortalService>();
        services.AddScoped<IVideoMeetingRoomService, VideoMeetingRoomService>();
        services.AddScoped<IMultiInboxRotationService, MultiInboxRotationService>();
        services.AddWhatsAppPairingServices();
        services.AddAgentFleetOrchestration();
        services.AddScoped<ITechnicalSeoCrawlerService, TechnicalSeoCrawlerService>();
        services.AddHttpClient<TechnicalSeoCrawlerService>();
        services.AddScoped<IP2PWarmupRescueService, P2PWarmupRescueService>();
        services.AddScoped<ICpqQuotationService, CpqQuotationService>();
        services.AddScoped<IB2BWholesalePortalService, B2BWholesalePortalService>();
        services.AddWebhookMesh();
        services.AddScoped<IPredictiveCadenceService, PredictiveCadenceService>();
        services.AddSingleton<IInventoryReplenishmentService, InventoryReplenishmentService>();
        services.AddSingleton<IWhitelabelBrandingService, WhitelabelBrandingService>();
        services.AddScoped<ICashflowForecastingService, CashflowForecastingService>();
        services.AddSingleton<IDistributedRateLimiterService, DistributedRateLimiterService>();
        services.AddScoped<ICustomerRetentionDaemonService, CustomerRetentionDaemonService>();
        services.AddSingleton<IMerkleAuditLedgerService, MerkleAuditLedgerService>();
        services.AddScoped<IFeedbackBoardService, FeedbackBoardService>();
        services.AddScoped<ITaxAndInvoiceLedgerService, TaxAndInvoiceLedgerService>();
        services.AddScoped<IHybridRagAutopilotService, HybridRagAutopilotService>();
        services.AddScoped<IDocumentTelemetryHeatmapService, DocumentTelemetryHeatmapService>();
        services.AddSingleton<IDynamicPricingBanditService, DynamicPricingBanditService>();
        services.AddSingleton<ILeadEnrichmentRadarService, LeadEnrichmentRadarService>();
        services.AddSingleton<IPredictiveSlaMonitorService, PredictiveSlaMonitorService>();
        services.AddSingleton<IDistributedIdempotencyService, DistributedIdempotencyService>();
        services.AddSingleton<IFunnelSplitTestingBanditService, FunnelSplitTestingBanditService>();
        services.AddSingleton<IContinuousComplianceAuditorService, ContinuousComplianceAuditorService>();
        services.AddSingleton<ICustomEmailDomainService, CustomEmailDomainService>();
        services.AddSingleton<IContractElectronicSignatureService, ContractElectronicSignatureService>();
        services.AddSingleton<IDistributedEventMeshService, DistributedEventMeshService>();
        services.AddSingleton<IServerSideAttributionService, ServerSideAttributionService>();
        services.AddSingleton<IAppointmentSchedulingService, AppointmentSchedulingService>();
        services.AddSingleton<ICustomerLifecycleJourneyService, CustomerLifecycleJourneyService>();
        services.AddSingleton<IProductAnalyticsService, ProductAnalyticsService>();

        services.AddHttpClient("WebScraperClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "SGrowing-Bot/2.0 (Resilient Scraper Engine)");
        });

        return services;
    }

    public static IServiceCollection AddSecurityAndAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:SecretKey"] ?? "SGrowing_SuperSecretEnterpriseMasterKey_998877665544332211!!";
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "SGrowing.Api";
        var jwtAudience = configuration["Jwt:Audience"] ?? "SGrowing.Clients";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
        services.AddAuthorization();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<DatabaseSeeder>();
        services.AddOpenApi();

        return services;
    }
}
