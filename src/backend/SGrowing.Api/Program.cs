using Scalar.AspNetCore;
using SGrowing.Api.Modules.Attribution.MultiTouch;
using SGrowing.Api.Modules.Funnels.MabOptimizer;
using SGrowing.Api.Modules.Deliverability.WarmupSwarm;
using SGrowing.Api.Modules.CRM.MetadataEngine;
using SGrowing.Api.Modules.Automation.ExecutionEngine;
using SGrowing.Api.Modules.Billing.DunningEngine;
using SGrowing.Api.Modules.AI.SwarmCoordination;
using SGrowing.Api.Modules.Integrations.Marketplace;
using SGrowing.Api.Modules.Voice.WebRtcSdr;
using SGrowing.Api.Modules.Analytics.RevenueIntelligence;
using SGrowing.Api.Modules.Tenancy.Licensing;
using SGrowing.Api.Modules.SEO.GeoVisibility;
using SGrowing.Api.Modules.Channels.WhatsAppBroadcast;
using SGrowing.Api.Modules.AI.VectorRag;
using SGrowing.Api.Modules.SEO.Programmatic;
using SGrowing.Api.Modules.Billing.TaxCompliance;
using SGrowing.Api.Modules.Sales.DealCopilot;
using SGrowing.Api.Modules.CMS.ContentFactory;
using SGrowing.Api.Modules.Growth.Affiliates;
using SGrowing.Api.Modules.Voice.CallCopilot;
using SGrowing.Api.Modules.Sales.TerritoryRouting;
using SGrowing.Api.Modules.Security.ComplianceVault;
using SGrowing.Api.Modules.Journeys.LifecycleOrchestrator;
using SGrowing.Api.Modules.Funnels.CanvasBuilder;
using SGrowing.Api.Modules.Analytics.BiReportBuilder;
using SGrowing.Api.Modules.Dev.WebhookMesh;
using SGrowing.Api.Modules.Ai.KnowledgeGraph;
using SGrowing.Api.Modules.Voice.IvrTree;
using SGrowing.Api.Modules.Commerce.FraudFirewall;
using SGrowing.Api.Modules.Security.CollaborationAudit;
using SGrowing.Api.Modules.Dev.AppGenerator;
using SGrowing.Api.Modules.Tenancy.CustomDomains;
using SGrowing.Api.Modules.AI.VideoAvatar;
using SGrowing.Api.Modules.Social.AutoTriage;
using SGrowing.Api.Modules.Contracts.ChurnRescue;
using SGrowing.Api.Modules.Growth.Influencers;
using SGrowing.Api.Modules.Advertising.SpendAutopilot;
using SGrowing.Api.Modules.Analytics.DataCleanRoom;
using SGrowing.Api.Modules.Channels.FlowInteractive;
using SGrowing.Api.Modules.Infrastructure.SelfHealing;
using SGrowing.Api.Modules.Intelligence.DeAnonymization;
using SGrowing.Api.Modules.Advertising.AudienceSync;
using SGrowing.Api.Modules.Voice.ObjectionScoring;
using SGrowing.Api.Modules.SEO.TopicalAuthority;
using SGrowing.Api.Modules.Infrastructure.FeatureFlags;
using SGrowing.Api.Modules.Intelligence.WaterfallEnrichment;
using SGrowing.Api.Modules.Content.SocialProof;
using SGrowing.Api.Modules.Commerce.CartRescue;
using SGrowing.Api.Modules.Partnerships.DealRegistration;
using SGrowing.Api.Modules.Infrastructure.SemanticCache;
using SGrowing.Api.Modules.Intelligence.BuyingCommittee;
using SGrowing.Api.Modules.Advertising.CreativeBandit;
using SGrowing.Api.Modules.Commerce.CpqMargin;
using SGrowing.Api.Modules.Support.Localization;
using SGrowing.Api.Modules.Infrastructure.DisasterRecovery;
using SGrowing.Api.Modules.Content.Repurposing;
using SGrowing.Api.Modules.Analytics.ShapleyAttribution;
using SGrowing.Api.Modules.Voice.AgentAssist;
using SGrowing.Api.Modules.Security.EphemeralVault;
using SGrowing.Api.Modules.Intelligence.WinLoss;
using SGrowing.Api.Modules.Advertising.RetargetingMatrix;
using SGrowing.Api.Modules.Support.SentimentDeescalation;
using SGrowing.Api.Modules.Billing.ConsumptionMetering;
using SGrowing.Api.Modules.Intelligence.MutualActionPlan;
using SGrowing.Api.Modules.Advertising.CatalogFeeds;
using SGrowing.Api.Modules.Support.CapacityPlanning;
using SGrowing.Api.Modules.Intelligence.AccountExpansion;
using SGrowing.Api.Modules.Commerce.InfluencerEscrowV2;
using SGrowing.Api.Modules.Voice.CallTransferMesh;
using SGrowing.Api.Modules.Finance.Asc606Revenue;
using SGrowing.Api.Modules.Security.SecretVaultMesh;
using SGrowing.Api.Modules.Intelligence.PipelineVelocity;
using SGrowing.Api.Modules.Advertising.BidPacing;
using SGrowing.Api.Modules.Voice.CallCompliance;
using SGrowing.Api.Modules.Commerce.CpqApproval;
using SGrowing.Api.Modules.Marketing.AdSpendRedistribution;
using SGrowing.Api.Modules.Commerce.CommissionSettlement;
using SGrowing.Api.Modules.Sales.TerritoryRebalancing;
using SGrowing.Api.Modules.Support.ScreenDiagnostic;
using SGrowing.Api.Modules.Sales.IntentRollup;
using SGrowing.Api.Modules.Voice.AgentWhisper;
using SGrowing.Api.Modules.Infrastructure.DbQueryRouter;
using SGrowing.Api.Modules.Personalization.ContentPersonalization;
using SGrowing.Api.Modules.Advertising.VideoDco;
using SGrowing.Api.Modules.Voice.EmotionDeescalation;
using SGrowing.Api.Modules.Sales.DirectMailMesh;
using SGrowing.Api.Modules.Voice.SpeechStreamingMesh;
using SGrowing.Api.Modules.Sales.CollaborativeDealRoom;
using SGrowing.Api.Modules.Voice.MultilingualVoiceMesh;
using SGrowing.Api.Modules.Sales.CompetitiveBattlecard;
using SGrowing.Api.Modules.Voice.CallStateResumption;
using SGrowing.Api.Modules.Sales.RfpResponse;
using SGrowing.Api.Modules.Advertising.GeoIncrementality;
using SGrowing.Api.Modules.Sales.BuyingCommittee;
using SGrowing.Api.Modules.Voice.TelephonyWhisper;
using SGrowing.Api.Modules.Sales.ContractRedline;
using SGrowing.Api.Modules.Sales.CustomerAdvisoryBoard;
using SGrowing.Api.Modules.Voice.VoiceIntentFastPath;
using SGrowing.Api.Modules.Sales.ValueRealization;
using SGrowing.Api.Modules.Sales.CustomerAdvocacy;
using SGrowing.Api.Modules.Sales.DealCoaching;
using SGrowing.Api.Modules.Sales.SpiffGamification;
using SGrowing.Api.Modules.Sales.DealAutopsy;
using SGrowing.Api.Modules.Advertising.SponsoredBrandAds;
using SGrowing.Api.Modules.Sales.RepRamp;
using SGrowing.Api.Modules.Advertising.CrossDeviceSync;
using SGrowing.Api.Modules.Sales.QuotaCapacity;
using SGrowing.Api.Modules.Voice.DialectSynthesis;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
using SGrowing.Api.Extensions;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Agents;
using SGrowing.Api.Modules.Agents.Git;
using SGrowing.Api.Modules.Agents.GrowthDirector;
using SGrowing.Api.Modules.Agents.Swarm;
using SGrowing.Api.Modules.Commerce.DynamicPricing;
using SGrowing.Api.Modules.Commerce.LiveStreaming;
using SGrowing.Api.Modules.Security.GraphFraud;
using SGrowing.Api.Modules.Conversations.Unified;
using SGrowing.Api.Modules.Sales.Proposals;
using SGrowing.Api.Modules.Documents.Idp;
using SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;
using SGrowing.Api.Modules.Tenancy.MultiRegion;
using SGrowing.Api.Modules.Marketing.Retention;
using SGrowing.Api.Modules.Analytics.SessionReplay;
using SGrowing.Api.Modules.Marketing.Copywriting;
using SGrowing.Api.Modules.Resilience.Chaos;
using SGrowing.Api.Modules.Commerce.VisualSearch;
using SGrowing.Api.Modules.Voice.Synthesis;
using SGrowing.Api.Modules.CRM.SupportSwarm;
using SGrowing.Api.Modules.Marketing.AdSpend;
using SGrowing.Api.Modules.Tenancy.Governance;
using SGrowing.Api.Modules.Commerce.CoOp;
using SGrowing.Api.Modules.CRM.RetentionConcierge;
using SGrowing.Api.Modules.Revenue.YieldManagement;
using SGrowing.Api.Modules.Security.VoiceLiveness;


using SGrowing.Api.Modules.AI.SyntheticData;
using SGrowing.Api.Modules.Tenancy.DataSovereignty;



using SGrowing.Api.Modules.AI.DocumentGrounding;









using SGrowing.Api.Modules.CustomerData.IdentityResolution;
using SGrowing.Api.Modules.CustomerData.Firmographics;
using SGrowing.Api.Modules.Advertising.MetaGoogleAdsSync;
using SGrowing.Api.Modules.Automation.WorkflowTelemetry;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.AI.Governance;
using SGrowing.Api.Modules.AI.Firewall;
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
using SGrowing.Api.Modules.Channels.WhatsApp.Companion;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Commerce.Affiliates;
using SGrowing.Api.Modules.Commerce.AntiFraud;
using SGrowing.Api.Modules.Commerce.CallCenter;
using SGrowing.Api.Modules.Commerce.CartReservations;
using SGrowing.Api.Modules.Commerce.Connectors;
using SGrowing.Api.Modules.Contracts;
using SGrowing.Api.Modules.Conversations;
using SGrowing.Api.Modules.Conversations.Macros;
using SGrowing.Api.Modules.Conversations.Mime;
using SGrowing.Api.Modules.Conversations.Presence;
using SGrowing.Api.Modules.Conversations.PresenceAndCsat;
using SGrowing.Api.Modules.Conversations.Sla;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.CRM.ContractSigning;
using SGrowing.Api.Modules.CRM.CustomFields;
using SGrowing.Api.Modules.CRM.CustomObjects;
using SGrowing.Api.Modules.CRM.Import;
using SGrowing.Api.Modules.Forms;
using SGrowing.Api.Modules.Deliverability;
using SGrowing.Api.Modules.Dev;
using SGrowing.Api.Modules.Dev.Gateway;
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
using SGrowing.Api.Modules.Journeys.OmnichannelSequence;
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
using SGrowing.Api.Modules.Security.Vault;
using SGrowing.Api.Modules.SEO;
using SGrowing.Api.Modules.Social;
using SGrowing.Api.Modules.Storage.Media;
using SGrowing.Api.Modules.Telemetry;
using SGrowing.Api.Modules.Tenancy;
using SGrowing.Api.Modules.Tenancy.SnapshotsAndRebilling;
using SGrowing.Api.Modules.Voice;
using SGrowing.Api.Modules.Voice.Streaming;
using SGrowing.Api.Modules.Conversations.Incidents;
using SGrowing.Api.Modules.Conversations.Hierarchy;
using SGrowing.Api.Modules.Notifications;
using SGrowing.Api.Modules.CRM.Pipeline;
using SGrowing.Api.Modules.Documents.Security;
using SGrowing.Api.Modules.Social.Listening;
using SGrowing.Api.Modules.Analytics.PredictiveLtv;
using SGrowing.Api.Modules.Voice.IVR;
using SGrowing.Api.Modules.Security.SyntheticData;
using SGrowing.Api.Modules.Search.Universal;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// 1. Tenancy, Database & PostgreSql RLS
builder.Services.AddDatabaseAndTenancy(builder.Configuration);

// 2. Core Messaging, Redis, SignalR & Outbox
builder.Services.AddCoreAndMessagingServices(builder.Configuration);

// 3. Omnichannel Commerce, COD, WMS & Logistics
builder.Services.AddOmnichannelAndCommerceServices();

// 4. Enterprise Horizons, AI Agents & Advanced Features
builder.Services.AddEnterpriseHorizonsServices();

// 5. Authentication, Authorization & Security
builder.Services.AddSecurityAndAuthServices(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();

// Middleware Pipeline
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseCors();
app.UseRateLimiter();
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

// ----------------- Documentation & OpenAPI -----------------
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("SGrowing Omnichannel & AI Growth Engine API")
           .WithTheme(ScalarTheme.Moon)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// ----------------- Modular Endpoint Registration -----------------
app.MapEndpointModules(typeof(Program).Assembly);

// ----------------- Enterprise Strategic Horizon Endpoints -----------------
app.MapProgrammaticSeoEndpoints();
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
app.MapDeliveryWebhookEndpoints();
app.MapProjectTaskEndpoints();
app.MapDeepWebResearchEndpoints();
app.MapOrganizationHierarchyEndpoints();
app.MapWhatsAppGatewayEndpoints();
app.MapAutonomousFunnelEndpoints();
app.MapAgentPersonaEndpoints();
app.MapMediaVaultEndpoints();
app.MapInvoiceEndpoints();
app.MapPaymentWebhookEndpoints();
app.MapConversationCopilotEndpoints();
app.MapCustomFieldEndpoints();
app.MapCustomObjectEndpoints();
app.MapWorkflowGraphEndpoints();
app.MapWorkflowDagReplayEndpoints();
app.MapCartReservationEndpoints();
app.MapEventFanoutEndpoints();
app.MapWhatsAppNewsletterEndpoints();
app.MapWhatsAppBroadcastPacingEndpoints();
app.MapWhatsAppCatalogCartEndpoints();
app.MapWhatsAppClusterAndPacingEndpoints();
app.MapTelegramBotEndpoints();
app.MapAiModelRouterEndpoints();
app.MapPromptFirewallEndpoints();
app.MapAiGovernanceEndpoints();

// Enterprise Wiki Strategic Pillars Endpoints (Waves 1-4)
app.MapCallCenterAntiCollisionEndpoints();
app.MapCodNetMarginEndpoints();
app.MapConversationPresenceEndpoints();
app.MapCsatSurveyEndpoints();
app.MapTenantSnapshotEndpoints();
app.MapTenantRebillingEndpoints();
SGrowing.Api.Modules.Outbound.Enrichment.WaterfallEnrichmentEndpoints.MapWaterfallEnrichmentEndpoints(app);
app.MapSeoMultipageGraphEndpoints();
app.MapSerpRankTrackerEndpoints();
app.MapInAppMicroSurveyEndpoints();
app.MapAndroidSmsGatewayEndpoints();
app.MapVoiceCallAmdEndpoints();
app.MapVoiceDiarizationEndpoints();
app.MapVoiceAiStreamingEndpoints();
app.MapCommunityFeedbackEndpoints();

// Core Inspiration Repositories Endpoint Mappings (Phases 1-4)
app.MapChatbotFlowEndpoints();
app.MapInAppEngagementEndpoints();
app.MapInAppTourEndpoints();
app.MapCollisionAndSlaEndpoints();
app.MapUnifiedDeskEndpoints();
app.MapWarmupNetworkEndpoints();
app.MapAutonomousSdrFleetEndpoints();
SGrowing.Api.Modules.Marketing.SocialPublishingEndpoints.MapMarketingSocialPublishingEndpoints(app);
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

// Horizons 10-13 Endpoint Mappings
SGrowing.Api.Modules.Marketing.SocialPublishing.SocialPublishingEndpoints.MapSocialPublishingEndpoints(app);
app.MapWhatsAppPairingEndpoints();
app.MapWhatsAppCompanionEndpoints();
app.MapAgentFleetEndpoints();
app.MapTechnicalSeoCrawlerEndpoints();

// Horizon 16-52 Endpoint Mappings
app.MapWarmupRescueEndpoints();
app.MapCustomer360Endpoints();
app.MapB2BWholesaleEndpoints();
app.MapCpqQuotationEndpoints();
app.MapWebhookMeshEndpoints();
app.MapPredictiveCadenceEndpoints();
app.MapInventoryReplenishmentEndpoints();
app.MapWhitelabelBrandingEndpoints();
app.MapCashflowForecastingEndpoints();
app.MapRateLimiterEndpoints();
app.MapRetentionEndpoints();
app.MapMerkleAuditEndpoints();
app.MapFeedbackBoardEndpoints();
app.MapTaxInvoiceEndpoints();
app.MapHybridRagEndpoints();
app.MapDocumentTelemetryEndpoints();
app.MapDynamicPricingEndpoints();
app.MapLeadEnrichmentEndpoints();
app.MapSlaMonitorEndpoints();
app.MapIdempotencyEndpoints();
app.MapFunnelSplitTestingEndpoints();
app.MapComplianceAuditorEndpoints();
app.MapCustomEmailDomainEndpoints();
app.MapContractSignatureEndpoints();
app.MapEventMeshEndpoints();
app.MapServerSideAttributionEndpoints();
app.MapAppointmentSchedulingEndpoints();
SGrowing.Api.Modules.Social.SocialPublishingEndpoints.MapSocialPublishingEndpoints(app);
app.MapCustomerLifecycleJourneyEndpoints();
app.MapProductAnalyticsEndpoints();
app.MapAgentPresenceEndpoints();
app.MapJourneyEndpoints();
app.MapJourneyDagEndpoints();
app.MapHeadlessFormEndpoints();
app.MapAffiliateCommissionEndpoints();
app.MapIncidentOnCallEndpoints();
app.MapNotificationEndpoints();
app.MapConversationHierarchyEndpoints();
app.MapDocumentSecurityEndpoints();
app.MapDealSlippageRadarEndpoints();
app.MapSocialListeningEndpoints();
app.MapPredictiveLtvEndpoints();
app.MapVoiceIvrEndpoints();
app.MapSyntheticDataEndpoints();
app.MapUniversalSearchEndpoints();
app.MapComplianceVaultEndpoints();

// Horizons 16-20 Endpoint Mappings
app.MapMultiAgentSwarmEndpoints();
app.MapGrowthDirectorEndpoints();
app.MapDynamicElasticityPricingEndpoints();
app.MapLiveCommerceEndpoints();

// Horizons 21-25 Endpoint Mappings
app.MapIdentityGraphFraudEndpoints();
app.MapIdpEndpoints();
app.MapMicroFrontendEndpoints();

// Horizons 26-30 Endpoint Mappings
app.MapMultiRegionEndpoints();
app.MapPredictiveRetentionEndpoints();

// Horizons 31-35 Endpoint Mappings
app.MapSessionReplayEndpoints();
app.MapCopywritingEndpoints();
app.MapChaosResilienceEndpoints();

// Horizons 36-40 Endpoint Mappings
app.MapVisualSearchEndpoints();
app.MapVoiceSynthesisEndpoints();

// Horizons 41-45 Endpoint Mappings
app.MapSupportSwarmEndpoints();

// Horizons 46-50 Endpoint Mappings
app.MapAdSpendEndpoints();
app.MapGovernanceEndpoints();

// Horizons 51-55 Endpoint Mappings
app.MapCollaborativeCommerceEndpoints();

// Horizons 56-60 Endpoint Mappings
app.MapRetentionConciergeEndpoints();

// Horizons 61-65 Endpoint Mappings
app.MapYieldManagementEndpoints();
app.MapVoiceLivenessEndpoints();

// Horizons 66-70 Endpoint Mappings



// Horizons 71-75 Endpoint Mappings
app.MapDifferentialPrivacySandboxEndpoints();

// Horizons 76-80 Endpoint Mappings
app.MapDataSovereigntyEndpoints();


// Horizons 81-85 Endpoint Mappings

// Horizons 86-90 Endpoint Mappings


// Horizons 91-95 Endpoint Mappings

app.MapDocumentGroundingEndpoints();

// Domain B3 CDP Identity Resolution, Domain C6 Advertising Sync & Domain E1 Automation Visual DAG Telemetry
app.MapCdpIdentityEndpoints();
app.MapFirmographicsEndpoints();
app.MapAdsSyncEndpoints();
app.MapWorkflowTelemetryEndpoints();
app.MapOmnichannelSequenceEndpoints();
app.MapB2BProposalEndpoints();
app.MapUnifiedInboxEndpoints();
app.MapMultiTouchAttributionEndpoints();
app.MapAiGovernanceEndpoints();
app.MapMabOptimizerEndpoints();
app.MapWarmupSwarmEndpoints();
app.MapCrmMetadataEndpoints();
app.MapWorkflowExecutionEndpoints();
app.MapDunningBillingEndpoints();
app.MapSwarmCoordinationEndpoints();
app.MapAppMarketplaceEndpoints();
app.MapWebRtcSdrEndpoints();
app.MapRevenueIntelligenceEndpoints();
app.MapEnterpriseLicensingEndpoints();
app.MapGeoVisibilityEndpoints();
app.MapWhatsAppBroadcastEndpoints();
app.MapVectorRagEndpoints();
app.MapProgrammaticMeshEndpoints();
app.MapTaxComplianceEndpoints();
app.MapDealCopilotEndpoints();
app.MapContentFactoryEndpoints();
app.MapAffiliateLoyaltyEndpoints();
app.MapCallCopilotEndpoints();
app.MapTerritoryRoutingEndpoints();
app.MapZeroTrustVaultEndpoints();
app.MapLifecycleJourneyEndpoints();
app.MapCanvasBuilderEndpoints();
app.MapBiReportBuilderEndpoints();
app.MapBiDirectionalWebhookMeshEndpoints();
app.MapKnowledgeGraphEndpoints();
app.MapVoiceIvrTreeEndpoints();
app.MapFraudFirewallEndpoints();
app.MapCollaborationAuditEndpoints();
app.MapAppGeneratorEndpoints();
app.MapCustomDomainEndpoints();
app.MapVideoAvatarEndpoints();
app.MapSocialTriageEndpoints();
app.MapChurnRescueEndpoints();
app.MapInfluencerEscrowEndpoints();
app.MapAdSpendAutopilotEndpoints();
app.MapDataCleanRoomEndpoints();
app.MapFlowInteractiveEndpoints();
app.MapSelfHealingMeshEndpoints();
app.MapDeAnonymizationEndpoints();
app.MapAudienceSyncEndpoints();
app.MapObjectionScoringEndpoints();
app.MapTopicalAuthorityEndpoints();
app.MapFeatureFlagEndpoints();
app.MapLeadWaterfallEnrichmentEndpoints();
app.MapSocialProofEndpoints();
app.MapCartRescueEndpoints();
app.MapDealRegistrationEndpoints();
app.MapSemanticCacheEndpoints();
app.MapBuyingCommitteeEndpoints();
app.MapCreativeBanditEndpoints();
app.MapCpqMarginEndpoints();
app.MapSupportLocalizationEndpoints();
app.MapDisasterRecoveryEndpoints();
app.MapContentRepurposingEndpoints();
app.MapShapleyAttributionEndpoints();
app.MapAgentAssistEndpoints();
app.MapEphemeralVaultEndpoints();
app.MapWinLossEndpoints();
app.MapRetargetingMatrixEndpoints();
app.MapConsumptionMeteringEndpoints();
app.MapMutualActionPlanEndpoints();
app.MapCatalogFeedsEndpoints();
app.MapCapacityPlanningEndpoints();
app.MapAccountExpansionEndpoints();
app.MapInfluencerEscrowV2Endpoints();
app.MapCallTransferMeshEndpoints();
app.MapAsc606RevenueEndpoints();
app.MapSecretVaultMeshEndpoints();
app.MapPipelineVelocityEndpoints();
app.MapBidPacingEndpoints();
app.MapCallComplianceEndpoints();
app.MapCpqApprovalEndpoints();
app.MapAdSpendRedistributionEndpoints();
app.MapCommissionSettlementEndpoints();
app.MapTerritoryRebalancingEndpoints();
app.MapScreenDiagnosticEndpoints();
app.MapSentimentDeescalationEndpoints();
app.MapIntentRollupEndpoints();
app.MapAgentWhisperEndpoints();
app.MapDbQueryRouterEndpoints();
app.MapContentPersonalizationEndpoints();
app.MapVideoDcoEndpoints();
app.MapEmotionDeescalationEndpoints();
app.MapDirectMailMeshEndpoints();
app.MapSpeechStreamingMeshEndpoints();
app.MapCollaborativeDealRoomEndpoints();
app.MapMultilingualVoiceMeshEndpoints();
app.MapCompetitiveBattlecardEndpoints();
app.MapCallStateResumptionEndpoints();
app.MapRfpResponseEndpoints();
app.MapGeoIncrementalityEndpoints();
app.MapBuyingCommitteeGraphEndpoints();
app.MapTelephonyWhisperEndpoints();
app.MapContractRedlineEndpoints();
app.MapCustomerAdvisoryBoardEndpoints();
app.MapVoiceIntentFastPathEndpoints();
app.MapValueRealizationEndpoints();
app.MapCustomerAdvocacyEndpoints();
app.MapDealCoachingEndpoints();
app.MapSpiffGamificationEndpoints();
app.MapDealAutopsyEndpoints();
app.MapSponsoredBrandAdsEndpoints();
app.MapRepRampEndpoints();
app.MapCrossDeviceSyncEndpoints();
app.MapQuotaCapacityEndpoints();
app.MapDialectSynthesisEndpoints();

// Real-Time SignalR Hub
app.MapHub<OmniHub>("/hubs/omni");
app.MapGrowthOsHubEndpoints();

// Optional CLI Demo Seeding
if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedEnterpriseDemoAsync();
    Console.WriteLine("Demo database successfully seeded.");
}

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
