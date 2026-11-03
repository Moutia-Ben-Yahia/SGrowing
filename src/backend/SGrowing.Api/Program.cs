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

// ----------------- Core Modular Slice Endpoints -----------------
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapCrmEndpoints();
app.MapConversationsEndpoints();
app.MapOutboundSdrEndpoints();
app.MapJourneysFeedbackEndpoints();
app.MapChannelsMessagingEndpoints();
app.MapCommerceEndpoints();
app.MapMarketingEndpoints();
app.MapVoiceEndpoints();
app.MapAiGatewayEndpoints();
app.MapFinanceEndpoints();
app.MapDeveloperGatewayEndpoints();

// ----------------- Enterprise Strategic Horizon Endpoints -----------------
app.MapDevEndpoints();
app.MapSeoEndpoints();
app.MapProgrammaticSeoEndpoints();
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

// Horizons 96-100 Endpoint Mappings (Century Capstone Wave)

// Horizons 101-105 Endpoint Mappings (Next-Era Wave)

// Horizons 106-110 Endpoint Mappings (Deep Infrastructure Wave)

// Horizons 111-115 Endpoint Mappings (Precision Decarbonization & Cyber-Physical Systems)



// Horizons 116-120 Endpoint Mappings (Nuclear SMR, Hypersonic Aerospace, QKD, Global Trade & ZK Biometrics)


// Horizons 121-125 Endpoint Mappings (Fusion Tokamak MHD, BCI Kinematics, Gravitational Interferometer, EUV Pellicle & HFT Microstructure)


// Horizons 126-130 Endpoint Mappings (Deep-Sea Submersible, Cryo Turbopump, CRISPR Cleavage, Ring Signatures & UHV Corona)

// Horizons 131-135 Endpoint Mappings (Solid-State Battery EIS, Subsea DWDM Raman, Superconducting Qubit Gates, Aerocapture Guidance & Threshold ML-DSA)

// Horizons 136-140 Endpoint Mappings (MRI Quench Arbiter, Space Elevator Tether, PEM Electrolyzer, Spiking Retina & ZK State Relay)

// Horizons 141-145 Endpoint Mappings (CV-QKD Coherent State, Molten Salt CSP Thermocline, Microbial CRISPR Cascade, Fusion Divertor Detachment & Hypersonic Waverider Shock)

// Horizons 146-150 Endpoint Mappings (Quantum Dot MEG, Hall Thruster Pole Erosion, Nanopore Basecaller, ZK L3 Watchtower & Superhot EGS)

// Horizons 151-155 Endpoint Mappings (THz 6G Beam Squint, Cryo Fusion Pellets, CAR-T Exhaustion, DMFC Poisoning & Deep-Space RTG Decay)

// Horizons 156-160 Endpoint Mappings (Gravitational Microlensing Exoplanets, HTS REBCO Delamination, Antibody Affinity Maturation, ZK Solvency & Hypersonic BLT)

// Horizons 161-165 Endpoint Mappings (SOEC Co-Electrolysis, OISL Laser Jitter, Spatial Transcriptomics, PQC-HTLC & Tokamak Runaway Suppression)

// Horizons 166-170 Endpoint Mappings (QCL Photoacoustic Spectroscopy, Scramjet Shock Train, CRISPR Prime Editing Flap, AMM LVR & Supercritical Wellbore)

// Horizons 171-175 Endpoint Mappings (Liquid Metal Blanket Hartmann Flow, Microbial Electrosynthesis, VASIMR Propulsion, OFA MEV & High-NA EUV)


// Horizons 176-180 Endpoint Mappings (sCO2 Brayton Compressor Surge, Direct-to-Cell LEO Doppler, CRISPR Cas12a DETECTR, ZK Dark Pool VDF & Lunar Regolith Microwave Sintering)

// Horizons 181-185 Endpoint Mappings (SMES Quench Fast Dump, Adaptive Optics Phase Conjugation, Cell-Free Protein Translation, DeFi Bad Debt Socialization & Hypersonic Skip-Glide)

// Horizons 186-190 Endpoint Mappings (CV-QKD Excess Noise, DAC Solid-Amine Chemisorption, Martian Aerocapture Guidance, HFT Micro-Price & LNG BOG Re-Liquefaction)

// Horizons 191-195 Endpoint Mappings (Kilopower Space Brayton, Neuromorphic STDP, API Polymorph Continuous Crystallization, Re-Staking Slashing & HEMP Substation Protection)

// Horizons 196-200 Endpoint Mappings (Double Century Milestone: Dilution Refrigerator ADR, Targeted Alpha TRT, Oblique Detonation Wave Engine, CBDC Atomic DvP & Neutrino Tomography)

// Horizons 201-205 Endpoint Mappings (MBE RHEED Growth, CRISPR Base Editing Bystander, Stratospheric HALE Solar Balance, ZK Recursive Folding & MCFC Carbon Capture)


// Horizons 206-210 Endpoint Mappings (Maglev Inductrack EDS, Optical Coherence Elastography, Superconducting Marine MHD, ZK Rollup Censorship Resistance & Supercritical Thermosiphon Geothermal)


// Horizons 211-215 Endpoint Mappings (Microfluidic LNP mRNA, QCL Atmospheric Plume, Dual-Stage Hall Thruster, ZK Solvency Audit & Tokamak Liquid Lithium CPS Divertor)

// Horizons 216-220 Endpoint Mappings (Strontium-87 Optical Clock, AMD Bioleaching, Aerogel TPS Ablation, LSD Depeg Arbitrage & Hyperloop Kantrowitz Aerodynamics)


// Horizons 221-225 Endpoint Mappings (STED Super-Resolution, PACE Continuous Evolution, M2P2 Plasma Propulsion, Sovereign Debt Climate Resilience & Seismic Metamaterial Cloak)

// Horizons 226-230 Endpoint Mappings (Photonic GBS, Epigenetic Clock, Electrodynamic Tether, VPIN Toxic Flow & Adiabatic CAES)

// Horizons 231-235 Endpoint Mappings (Cold Atom Gravimeter, IVT mRNA Capping, Aerogravity-Assist, Cross-Asset Spillover & SCWO PFAS Elimination)

// Horizons 236-240 Endpoint Mappings (Neutral Atom Rydberg Gates, CAR-T Exhaustion Reprogramming, Solar Sail Radiation Pressure, Dynamic Margin Collateral VaR & Microbial Fuel Cell Bioenergy)

// Horizons 241-245 Endpoint Mappings (NV Center Magnetometry, Prime Editing Search-and-Replace, MHD Boundary Layer Flow Control, AMM Impermanent Loss Dynamic Hedge & Direct Methanol Fuel Cell)

// Horizons 246-250 Endpoint Mappings (Topological Majorana Braiding, CRISPR RNP Electroporation, Nuclear Thermal Propulsion, Cross-Currency Flash Swap Arbitrage & Ocean Thermal Energy Conversion)

// Horizons 251-255 Endpoint Mappings (Cavity Optomechanics Phonon Squeezing, AAV Capsid Evolution, Rotating Detonation Combustor, Order Flow Adverse Selection & Alkaline Water Electrolysis)

// Horizons 256-260 Endpoint Mappings (Superconducting Fluxonium Coherence, Cytidine Base Editing Bystander, Hypersonic Aerocapture, Ornstein-Uhlenbeck Stat Arb & SOEC Co-Electrolysis)

// Horizons 261-265 Endpoint Mappings (NV Center Nuclear Spin Register, Lentiviral Integration Oncogenesis, Lunar Crater Tether, SABR Volatility Surface & LFP Battery Thermal Runaway)

// Horizons 266-270 Endpoint Mappings (Bosonic Cat Qubit Protection, Engineered Exosome Cargo, Asteroid Kinetic Impactor, Hierarchical Risk Parity & VRFB Spectrophotometry Balancing)

// Horizons 271-275 Endpoint Mappings (TWPA Readout, PROTAC Kinetics, Scramjet Isolator, FX Basis CIP & Compressed Gas Geothermal)

// Horizons 276-280 Endpoint Mappings (Neutral Atom Tweezer Rearrangement, Cell-Free Protein Synthesis, Hall-Effect Thruster Erosion, Microstructure Kyle's Lambda & Solid-State Battery Dendrite Suppression)

// Horizons 281-285 Endpoint Mappings (Diamond Color Center Nanophotonics, Oligo Synthesis, Dual-Comb Spectroscopy, Merton Jump Diffusion & Perovskite Tandem)

// Horizons 286-290 Endpoint Mappings (Superconducting Crosstalk Cancellation, Single-Cell Ambient RNA Decontamination, Reaction Wheel Jitter, Bond Portfolio Immunization & Molten Salt CSP)

// Horizons 291-295 Endpoint Mappings (Rare-Earth Optical Memory, HMW DNA Extraction, Lunar Orbit Insertion, CDO Copula Tranches & Solid Oxide Fuel Cells)

// Horizons 296-300 Endpoint Mappings (Silicon Spin Qubit Exchange, Nanopore Squiggle Basecalling, Aerobraking Drag, Almgren-Chriss Liquidation & Tokamak NTM ECCD Suppression)

// Horizons 301-305 Endpoint Mappings (CV Cluster Teleportation, Cryo-ET Subtomogram Averaging, Solar CME Shock Arrival, FX LSV Barrier Options & PEM Electrolyzer Gas Crossover)

// Horizons 306-310 Endpoint Mappings (Superconducting Quantum Acoustics, GUIDE-Seq Prime Editing, Atmospheric Entry Guidance, TIPS Real Yield & DAC TVSA Sorbent)

// Horizons 311-315 Endpoint Mappings (Majorana Zero Modes, Organoid Morphogenesis, OISL Crosslink PAT, CDS Index Tranche & Fischer-Tropsch SAF)

// Horizons 316-320 Endpoint Mappings (Surface Code Lattice Surgery, Droplet Single-Cell Microfluidics, Spacecraft Charging, Cross-Currency Basis & VRFB Shunt Currents)

// Horizons 321-325 Endpoint Mappings (Dual-Species Tweezers, CODEX Proteomics, Spacecraft RCS Plume Impingement, SVI Volatility Surface & SOEC Steam Electrolysis)

// Horizons 326-330 Core ERP Suite (MRP II, AP 3-Way Match, Warehouse Bin Cycle Counting, Multi-Tier Payroll & COD Settlement)


// Domain B3 CDP Identity Resolution, Domain C6 Advertising Sync & Domain E1 Automation Visual DAG Telemetry
app.MapCdpIdentityEndpoints();
app.MapFirmographicsEndpoints();
app.MapAdsSyncEndpoints();
app.MapWorkflowTelemetryEndpoints();
app.MapOmnichannelSequenceEndpoints();
app.MapDevGatewayEndpoints();
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
