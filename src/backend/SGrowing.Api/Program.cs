using Scalar.AspNetCore;
using SGrowing.Api.Modules.Attribution.MultiTouch;
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
using SGrowing.Api.Modules.Field.Sync;
using SGrowing.Api.Modules.Finance.Ledger;
using SGrowing.Api.Modules.SupplyChain.Replenishment;
using SGrowing.Api.Modules.DataArchitecture.EventSourcing;
using SGrowing.Api.Modules.Security.GraphFraud;
using SGrowing.Api.Modules.Conversations.Unified;
using SGrowing.Api.Modules.Sales.Proposals;
using SGrowing.Api.Modules.Documents.Idp;
using SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;
using SGrowing.Api.Modules.SupplyChain.Procurement;
using SGrowing.Api.Modules.Security.ZeroKnowledge;
using SGrowing.Api.Modules.Tenancy.MultiRegion;
using SGrowing.Api.Modules.Commerce.Robotics;
using SGrowing.Api.Modules.Marketing.Retention;
using SGrowing.Api.Modules.SupplyChain.Customs;
using SGrowing.Api.Modules.Analytics.SessionReplay;
using SGrowing.Api.Modules.Marketing.Copywriting;
using SGrowing.Api.Modules.Security.VerifiableIdentity;
using SGrowing.Api.Modules.Resilience.Chaos;
using SGrowing.Api.Modules.Logistics.Fleet;
using SGrowing.Api.Modules.Security.Confidential;
using SGrowing.Api.Modules.Commerce.VisualSearch;
using SGrowing.Api.Modules.Voice.Synthesis;
using SGrowing.Api.Modules.Infrastructure.Autopilot;
using SGrowing.Api.Modules.Finance.Factoring;
using SGrowing.Api.Modules.Logistics.ColdChain;
using SGrowing.Api.Modules.Commerce.QualityGate;
using SGrowing.Api.Modules.CRM.SupportSwarm;
using SGrowing.Api.Modules.Infrastructure.MultiCloud;
using SGrowing.Api.Modules.Logistics.Esg;
using SGrowing.Api.Modules.Security.PostQuantum;
using SGrowing.Api.Modules.Commerce.Shrinkage;
using SGrowing.Api.Modules.Marketing.AdSpend;
using SGrowing.Api.Modules.Tenancy.Governance;
using SGrowing.Api.Modules.Logistics.Satellite;
using SGrowing.Api.Modules.Commerce.Neuromorphic;
using SGrowing.Api.Modules.Finance.SettlementBridge;
using SGrowing.Api.Modules.Commerce.CoOp;
using SGrowing.Api.Modules.Infrastructure.EdgeObservability;
using SGrowing.Api.Modules.Commerce.DigitalTwin;
using SGrowing.Api.Modules.SupplyChain.ZkTraceability;
using SGrowing.Api.Modules.Logistics.Routing;
using SGrowing.Api.Modules.CRM.RetentionConcierge;
using SGrowing.Api.Modules.Security.ConfidentialEnclave;
using SGrowing.Api.Modules.Revenue.YieldManagement;
using SGrowing.Api.Modules.AI.KnowledgeHypergraph;
using SGrowing.Api.Modules.SupplyChain.DisruptionSimulator;
using SGrowing.Api.Modules.Security.VoiceLiveness;
using SGrowing.Api.Modules.Infrastructure.FinOps;
using SGrowing.Api.Modules.Sustainability.CarbonExchange;
using SGrowing.Api.Modules.Security.VerifiableCredentials;
using SGrowing.Api.Modules.IoT.PredictiveTelemetry;
using SGrowing.Api.Modules.SupplyChain.HsClassification;
using SGrowing.Api.Modules.Infrastructure.ServiceMesh;
using SGrowing.Api.Modules.Commerce.WarehouseSlotting;
using SGrowing.Api.Modules.Finance.FxHedging;
using SGrowing.Api.Modules.AI.SyntheticData;
using SGrowing.Api.Modules.Logistics.DroneCorridors;
using SGrowing.Api.Modules.Security.ZtnaContinuousPosture;
using SGrowing.Api.Modules.Tenancy.DataSovereignty;
using SGrowing.Api.Modules.SupplyChain.DemandSensing;
using SGrowing.Api.Modules.Voice.SentimentDeescalation;
using SGrowing.Api.Modules.IoT.ColdChainSla;
using SGrowing.Api.Modules.Security.PostQuantumCrypto;
using SGrowing.Api.Modules.Logistics.DrayageScheduler;
using SGrowing.Api.Modules.Security.HomomorphicAnalytics;
using SGrowing.Api.Modules.Finance.ConcentratedLiquidity;
using SGrowing.Api.Modules.Media.ProsodyDubbing;
using SGrowing.Api.Modules.Security.ZkRollupBridge;
using SGrowing.Api.Modules.Logistics.MaritimeEcoRouting;
using SGrowing.Api.Modules.Manufacturing.OpticalQualityControl;
using SGrowing.Api.Modules.Security.MpcThresholdSigner;
using SGrowing.Api.Modules.Sustainability.CircularRemanufacturing;
using SGrowing.Api.Modules.Finance.CreditRiskUnderwriting;
using SGrowing.Api.Modules.AI.DocumentGrounding;
using SGrowing.Api.Modules.Security.ZkIdentityVerification;
using SGrowing.Api.Modules.Infrastructure.DePinWorkerArbiter;
using SGrowing.Api.Modules.Finance.BepsPillarTwo;
using SGrowing.Api.Modules.Governance.EnterpriseImmuneSystem;
using SGrowing.Api.Modules.Finance.MicroPaymentMesh;
using SGrowing.Api.Modules.Logistics.RailHumpYardScheduler;
using SGrowing.Api.Modules.Security.ConfidentialRemoteAttestation;
using SGrowing.Api.Modules.Sustainability.MineralCarbonation;
using SGrowing.Api.Modules.Finance.CbdcCrossBorderBridge;
using SGrowing.Api.Modules.Manufacturing.CleanroomAirflowPredictor;
using SGrowing.Api.Modules.Security.QkdSatelliteArbiter;
using SGrowing.Api.Modules.Trade.RulesOfOriginTariff;
using SGrowing.Api.Modules.Security.ZkIrisBiometrics;
using SGrowing.Api.Modules.Manufacturing.EuvLithographyPellicle;
using SGrowing.Api.Modules.Finance.HftOrderBookMicrostructure;
using SGrowing.Api.Modules.Security.RingSignatureIdentity;
using SGrowing.Api.Modules.Security.ThresholdMlDsaSigner;
using SGrowing.Api.Modules.AI.SpikingRetinaGazeArbiter;
using SGrowing.Api.Modules.Finance.ZkStateRelayAggregator;
using SGrowing.Api.Modules.Security.CvQkdCoherentState;
using SGrowing.Api.Modules.Finance.ZkL3WatchtowerFraudProof;
using SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumTree;
using SGrowing.Api.Modules.Finance.PqcCrossChainHtlc;
using SGrowing.Api.Modules.Finance.AmmLossVersusRebalancing;
using SGrowing.Api.Modules.Finance.OrderFlowAuctionMev;
using SGrowing.Api.Modules.Manufacturing.HighNaEuvStochasticDefects;
using SGrowing.Api.Modules.Finance.ZkDarkPoolVdfBatching;
using SGrowing.Api.Modules.Finance.DeFiLendingBadDebtSocialization;
using SGrowing.Api.Modules.Security.CvQkdExcessNoiseSecretKeyRate;
using SGrowing.Api.Modules.Finance.HftVwapMicroPriceQueue;
using SGrowing.Api.Modules.AI.NeuromorphicStdpSynapticEnergy;
using SGrowing.Api.Modules.Finance.RestakingCryptoeconomicSlashing;
using SGrowing.Api.Modules.Finance.CbdcAtomicDvpLiquiditySaving;
using SGrowing.Api.Modules.Manufacturing.MbeRheedGrowthKinetics;
using SGrowing.Api.Modules.Security.ZkRecursiveFoldingScheme;
using SGrowing.Api.Modules.Transportation.MaglevInductrackNullFlux;
using SGrowing.Api.Modules.Finance.ZkRollupCensorshipResistance;
using SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumAudit;
using SGrowing.Api.Modules.Finance.LsdDepegArbitrageRedemption;
using SGrowing.Api.Modules.Transportation.HyperloopKantrowitzTubeAerodynamics;
using SGrowing.Api.Modules.Finance.SovereignDebtClimateResilience;
using SGrowing.Api.Modules.Infrastructure.SeismicMetamaterialPhononicCloak;
using SGrowing.Api.Modules.Finance.VpinMarketMakerToxicFlow;
using SGrowing.Api.Modules.Finance.CrossAssetLiquiditySpillover;
using SGrowing.Api.Modules.Finance.DynamicMarginCollateralVar;
using SGrowing.Api.Modules.Finance.AmmDynamicImpermanentLossHedge;
using SGrowing.Api.Modules.Finance.CrossCurrencyFlashSwapArbitrage;
using SGrowing.Api.Modules.Finance.OrderFlowAdverseSelectionDrain;
using SGrowing.Api.Modules.Finance.StatisticalArbitrageOrnsteinUhlenbeck;
using SGrowing.Api.Modules.Finance.SabrVolatilitySurfaceCalibration;
using SGrowing.Api.Modules.Finance.HierarchicalRiskParityClustering;
using SGrowing.Api.Modules.Finance.CrossCurrencyFxBasisSwapCip;
using SGrowing.Api.Modules.Finance.MicrostructureKylesLambdaAdverseSelection;
using SGrowing.Api.Modules.Finance.MertonJumpDiffusionOptionPricing;
using SGrowing.Api.Modules.Finance.BondPortfolioDurationConvexityImmunization;
using SGrowing.Api.Modules.Finance.CdoGaussianCopulaTrancheCorrelation;
using SGrowing.Api.Modules.Finance.AlmgrenChrissOptimalLiquidation;
using SGrowing.Api.Modules.Finance.FxLocalStochasticVolatility;
using SGrowing.Api.Modules.Finance.TipsRealYieldBreakevenInflation;
using SGrowing.Api.Modules.Finance.CdsIndexTrancheCorrelation;
using SGrowing.Api.Modules.Finance.CrossCurrencyBasisConvexity;
using SGrowing.Api.Modules.Finance.SviVolatilitySurfaceCalibration;
using SGrowing.Api.Modules.Manufacturing.MaterialRequirementsPlanning;
using SGrowing.Api.Modules.Finance.AccountsPayableThreeWayMatching;
using SGrowing.Api.Modules.SupplyChain.WarehouseBinCycleCounting;
using SGrowing.Api.Modules.Workforce.MultiTierPayrollEngine;
using SGrowing.Api.Modules.Logistics.CashOnDeliverySettlement;
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
using SGrowing.Api.Modules.Commerce.Logistics;
using SGrowing.Api.Modules.Commerce.Shipping;
using SGrowing.Api.Modules.Commerce.Warehouses;
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
using SGrowing.Api.Modules.Workforce.Shifts;
using SGrowing.Api.Modules.Commerce.Fulfillment;
using SGrowing.Api.Modules.CRM.Pipeline;
using SGrowing.Api.Modules.Documents.Security;
using SGrowing.Api.Modules.Social.Listening;
using SGrowing.Api.Modules.Analytics.PredictiveLtv;
using SGrowing.Api.Modules.Voice.IVR;
using SGrowing.Api.Modules.Security.SyntheticData;
using SGrowing.Api.Modules.Search.Universal;
using SGrowing.Api.Modules.Workforce.Timesheets;

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
app.MapProjectTaskEndpoints();
app.MapDeepWebResearchEndpoints();
app.MapWarehouseRoutingEndpoints();
app.MapFleetDispatchEndpoints();
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
app.MapWaterfallEnrichmentEndpoints();
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
app.MapCarrierDispatchEndpoints();

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
app.MapWorkforceShiftEndpoints();
app.MapDocumentSecurityEndpoints();
app.MapDealSlippageRadarEndpoints();
app.MapSocialListeningEndpoints();
app.MapSplitShipmentRoutingEndpoints();
app.MapPredictiveLtvEndpoints();
app.MapVoiceIvrEndpoints();
app.MapSyntheticDataEndpoints();
app.MapUniversalSearchEndpoints();
app.MapLogisticsAndReverseCodEndpoints();
app.MapComplianceVaultEndpoints();

// Horizons 16-20 Endpoint Mappings
app.MapMultiAgentSwarmEndpoints();
app.MapGrowthDirectorEndpoints();
app.MapDynamicElasticityPricingEndpoints();
app.MapDoubleEntryLedgerEndpoints();
app.MapCrdtDeltaSyncEndpoints();
app.MapLiveCommerceEndpoints();

// Horizons 21-25 Endpoint Mappings
app.MapSupplyChainEndpoints();
app.MapEventSourcingCqrsEndpoints();
app.MapIdentityGraphFraudEndpoints();
app.MapIdpEndpoints();
app.MapMicroFrontendEndpoints();

// Horizons 26-30 Endpoint Mappings
app.MapProcurementEndpoints();
app.MapZeroKnowledgeEndpoints();
app.MapMultiRegionEndpoints();
app.MapRoboticsEndpoints();
app.MapPredictiveRetentionEndpoints();

// Horizons 31-35 Endpoint Mappings
app.MapCustomsEndpoints();
app.MapSessionReplayEndpoints();
app.MapCopywritingEndpoints();
app.MapVerifiableIdentityEndpoints();
app.MapChaosResilienceEndpoints();

// Horizons 36-40 Endpoint Mappings
app.MapFleetEndpoints();
app.MapConfidentialEndpoints();
app.MapVisualSearchEndpoints();
app.MapVoiceSynthesisEndpoints();
app.MapAutopilotEndpoints();

// Horizons 41-45 Endpoint Mappings
app.MapFactoringEndpoints();
app.MapColdChainEndpoints();
app.MapQualityGateEndpoints();
app.MapSupportSwarmEndpoints();
app.MapMultiCloudEndpoints();

// Horizons 46-50 Endpoint Mappings
app.MapEsgEndpoints();
app.MapPostQuantumEndpoints();
app.MapShrinkageEndpoints();
app.MapAdSpendEndpoints();
app.MapGovernanceEndpoints();

// Horizons 51-55 Endpoint Mappings
app.MapSatelliteCommsEndpoints();
app.MapNeuromorphicVisionEndpoints();
app.MapSettlementBridgeEndpoints();
app.MapCollaborativeCommerceEndpoints();
app.MapEdgeObservabilityEndpoints();

// Horizons 56-60 Endpoint Mappings
app.MapDigitalTwinEndpoints();
app.MapZkTraceabilityEndpoints();
app.MapRouteOptimizationEndpoints();
app.MapRetentionConciergeEndpoints();
app.MapConfidentialEnclaveEndpoints();

// Horizons 61-65 Endpoint Mappings
app.MapYieldManagementEndpoints();
app.MapKnowledgeHypergraphEndpoints();
app.MapDisruptionSimulationEndpoints();
app.MapVoiceLivenessEndpoints();
app.MapFinOpsArbitrageEndpoints();

// Horizons 66-70 Endpoint Mappings
app.MapCarbonExchangeEndpoints();
app.MapVerifiableCredentialsEndpoints();
app.MapPredictiveTelemetryEndpoints();
app.MapHsClassificationEndpoints();
app.MapServiceMeshEndpoints();

// Horizons 71-75 Endpoint Mappings
app.MapWarehouseSlottingEndpoints();
app.MapFxHedgingEndpoints();
app.MapDifferentialPrivacySandboxEndpoints();
app.MapDroneCorridorEndpoints();
app.MapZtnaEndpoints();

// Horizons 76-80 Endpoint Mappings
app.MapDataSovereigntyEndpoints();
app.MapDemandSensingEndpoints();
app.MapVoiceSentimentDeescalationEndpoints();
app.MapColdChainSlaEndpoints();
app.MapPostQuantumCryptoEndpoints();

// Horizons 81-85 Endpoint Mappings
app.MapDrayageSchedulerEndpoints();
app.MapHomomorphicAnalyticsEndpoints();
app.MapConcentratedLiquidityEndpoints();
app.MapProsodyDubbingEndpoints();
app.MapZkRollupBridgeEndpoints();

// Horizons 86-90 Endpoint Mappings
app.MapMaritimeEcoRoutingEndpoints();
app.MapOpticalQualityControlEndpoints();
app.MapMpcThresholdSignerEndpoints();

// Horizons 91-95 Endpoint Mappings
app.MapCircularRemanufacturingEndpoints();
app.MapCreditRiskUnderwritingEndpoints();
app.MapDocumentGroundingEndpoints();
app.MapZkIdentityVerificationEndpoints();

// Horizons 96-100 Endpoint Mappings (Century Capstone Wave)
app.MapDePinEndpoints();
app.MapBepsPillarTwoEndpoints();
app.MapEnterpriseImmuneEndpoints();

// Horizons 101-105 Endpoint Mappings (Next-Era Wave)
app.MapMicroPaymentMeshEndpoints();

// Horizons 106-110 Endpoint Mappings (Deep Infrastructure Wave)
app.MapRailHumpYardEndpoints();
app.MapConfidentialRemoteAttestationEndpoints();

// Horizons 111-115 Endpoint Mappings (Precision Decarbonization & Cyber-Physical Systems)
app.MapMineralCarbonationEndpoints();
app.MapCbdcCrossBorderEndpoints();
app.MapCleanroomAirflowEndpoints();

// Horizons 116-120 Endpoint Mappings (Nuclear SMR, Hypersonic Aerospace, QKD, Global Trade & ZK Biometrics)
app.MapQkdSatelliteArbiterEndpoints();
app.MapRulesOfOriginTariffEndpoints();
app.MapZkIrisBiometricEndpoints();

// Horizons 121-125 Endpoint Mappings (Fusion Tokamak MHD, BCI Kinematics, Gravitational Interferometer, EUV Pellicle & HFT Microstructure)
app.MapEuvLithographyPellicleEndpoints();
app.MapHftOrderBookMicrostructureEndpoints();

// Horizons 126-130 Endpoint Mappings (Deep-Sea Submersible, Cryo Turbopump, CRISPR Cleavage, Ring Signatures & UHV Corona)
app.MapRingSignatureIdentityEndpoints();

// Horizons 131-135 Endpoint Mappings (Solid-State Battery EIS, Subsea DWDM Raman, Superconducting Qubit Gates, Aerocapture Guidance & Threshold ML-DSA)
app.MapThresholdMlDsaSignerEndpoints();

// Horizons 136-140 Endpoint Mappings (MRI Quench Arbiter, Space Elevator Tether, PEM Electrolyzer, Spiking Retina & ZK State Relay)
app.MapSpikingRetinaEndpoints();
app.MapZkStateRelayEndpoints();

// Horizons 141-145 Endpoint Mappings (CV-QKD Coherent State, Molten Salt CSP Thermocline, Microbial CRISPR Cascade, Fusion Divertor Detachment & Hypersonic Waverider Shock)
app.MapCvQkdEndpoints();

// Horizons 146-150 Endpoint Mappings (Quantum Dot MEG, Hall Thruster Pole Erosion, Nanopore Basecaller, ZK L3 Watchtower & Superhot EGS)
app.MapZkL3WatchtowerEndpoints();

// Horizons 151-155 Endpoint Mappings (THz 6G Beam Squint, Cryo Fusion Pellets, CAR-T Exhaustion, DMFC Poisoning & Deep-Space RTG Decay)

// Horizons 156-160 Endpoint Mappings (Gravitational Microlensing Exoplanets, HTS REBCO Delamination, Antibody Affinity Maturation, ZK Solvency & Hypersonic BLT)
app.MapZkSolvencyEndpoints();

// Horizons 161-165 Endpoint Mappings (SOEC Co-Electrolysis, OISL Laser Jitter, Spatial Transcriptomics, PQC-HTLC & Tokamak Runaway Suppression)
app.MapPqcHtlcEndpoints();

// Horizons 166-170 Endpoint Mappings (QCL Photoacoustic Spectroscopy, Scramjet Shock Train, CRISPR Prime Editing Flap, AMM LVR & Supercritical Wellbore)
app.MapAmmLvrEndpoints();

// Horizons 171-175 Endpoint Mappings (Liquid Metal Blanket Hartmann Flow, Microbial Electrosynthesis, VASIMR Propulsion, OFA MEV & High-NA EUV)
app.MapOrderFlowAuctionEndpoints();
app.MapHighNaEuvEndpoints();

// Horizons 176-180 Endpoint Mappings (sCO2 Brayton Compressor Surge, Direct-to-Cell LEO Doppler, CRISPR Cas12a DETECTR, ZK Dark Pool VDF & Lunar Regolith Microwave Sintering)
app.MapZkDarkPoolEndpoints();

// Horizons 181-185 Endpoint Mappings (SMES Quench Fast Dump, Adaptive Optics Phase Conjugation, Cell-Free Protein Translation, DeFi Bad Debt Socialization & Hypersonic Skip-Glide)
app.MapDeFiLendingEndpoints();

// Horizons 186-190 Endpoint Mappings (CV-QKD Excess Noise, DAC Solid-Amine Chemisorption, Martian Aerocapture Guidance, HFT Micro-Price & LNG BOG Re-Liquefaction)
app.MapCvQkdExcessNoiseEndpoints();
app.MapHftMicroPriceEndpoints();

// Horizons 191-195 Endpoint Mappings (Kilopower Space Brayton, Neuromorphic STDP, API Polymorph Continuous Crystallization, Re-Staking Slashing & HEMP Substation Protection)
app.MapNeuromorphicStdpEndpoints();
app.MapRestakingSlashingEndpoints();

// Horizons 196-200 Endpoint Mappings (Double Century Milestone: Dilution Refrigerator ADR, Targeted Alpha TRT, Oblique Detonation Wave Engine, CBDC Atomic DvP & Neutrino Tomography)
app.MapCbdcLiquiditySavingEndpoints();

// Horizons 201-205 Endpoint Mappings (MBE RHEED Growth, CRISPR Base Editing Bystander, Stratospheric HALE Solar Balance, ZK Recursive Folding & MCFC Carbon Capture)
app.MapMbeRheedGrowthEndpoints();
app.MapZkRecursiveFoldingEndpoints();

// Horizons 206-210 Endpoint Mappings (Maglev Inductrack EDS, Optical Coherence Elastography, Superconducting Marine MHD, ZK Rollup Censorship Resistance & Supercritical Thermosiphon Geothermal)
app.MapMaglevInductrackEndpoints();
app.MapZkRollupCensorshipEndpoints();

// Horizons 211-215 Endpoint Mappings (Microfluidic LNP mRNA, QCL Atmospheric Plume, Dual-Stage Hall Thruster, ZK Solvency Audit & Tokamak Liquid Lithium CPS Divertor)
app.MapZkSolvencyAuditEndpoints();

// Horizons 216-220 Endpoint Mappings (Strontium-87 Optical Clock, AMD Bioleaching, Aerogel TPS Ablation, LSD Depeg Arbitrage & Hyperloop Kantrowitz Aerodynamics)
app.MapLsdDepegArbitrageEndpoints();
app.MapHyperloopKantrowitzEndpoints();

// Horizons 221-225 Endpoint Mappings (STED Super-Resolution, PACE Continuous Evolution, M2P2 Plasma Propulsion, Sovereign Debt Climate Resilience & Seismic Metamaterial Cloak)
app.MapSovereignDebtEndpoints();
app.MapSeismicCloakEndpoints();

// Horizons 226-230 Endpoint Mappings (Photonic GBS, Epigenetic Clock, Electrodynamic Tether, VPIN Toxic Flow & Adiabatic CAES)
app.MapVpinEndpoints();

// Horizons 231-235 Endpoint Mappings (Cold Atom Gravimeter, IVT mRNA Capping, Aerogravity-Assist, Cross-Asset Spillover & SCWO PFAS Elimination)
app.MapCrossAssetSpilloverEndpoints();

// Horizons 236-240 Endpoint Mappings (Neutral Atom Rydberg Gates, CAR-T Exhaustion Reprogramming, Solar Sail Radiation Pressure, Dynamic Margin Collateral VaR & Microbial Fuel Cell Bioenergy)
app.MapDynamicMarginVarEndpoints();

// Horizons 241-245 Endpoint Mappings (NV Center Magnetometry, Prime Editing Search-and-Replace, MHD Boundary Layer Flow Control, AMM Impermanent Loss Dynamic Hedge & Direct Methanol Fuel Cell)
app.MapAmmDynamicHedgeEndpoints();

// Horizons 246-250 Endpoint Mappings (Topological Majorana Braiding, CRISPR RNP Electroporation, Nuclear Thermal Propulsion, Cross-Currency Flash Swap Arbitrage & Ocean Thermal Energy Conversion)
app.MapCrossCurrencyFlashSwapEndpoints();

// Horizons 251-255 Endpoint Mappings (Cavity Optomechanics Phonon Squeezing, AAV Capsid Evolution, Rotating Detonation Combustor, Order Flow Adverse Selection & Alkaline Water Electrolysis)
app.MapOrderFlowAdverseSelectionEndpoints();

// Horizons 256-260 Endpoint Mappings (Superconducting Fluxonium Coherence, Cytidine Base Editing Bystander, Hypersonic Aerocapture, Ornstein-Uhlenbeck Stat Arb & SOEC Co-Electrolysis)
app.MapStatisticalArbitrageEndpoints();

// Horizons 261-265 Endpoint Mappings (NV Center Nuclear Spin Register, Lentiviral Integration Oncogenesis, Lunar Crater Tether, SABR Volatility Surface & LFP Battery Thermal Runaway)
app.MapSabrVolatilityEndpoints();

// Horizons 266-270 Endpoint Mappings (Bosonic Cat Qubit Protection, Engineered Exosome Cargo, Asteroid Kinetic Impactor, Hierarchical Risk Parity & VRFB Spectrophotometry Balancing)
app.MapHierarchicalRiskParityEndpoints();

// Horizons 271-275 Endpoint Mappings (TWPA Readout, PROTAC Kinetics, Scramjet Isolator, FX Basis CIP & Compressed Gas Geothermal)
app.MapCrossCurrencyFxBasisEndpoints();

// Horizons 276-280 Endpoint Mappings (Neutral Atom Tweezer Rearrangement, Cell-Free Protein Synthesis, Hall-Effect Thruster Erosion, Microstructure Kyle's Lambda & Solid-State Battery Dendrite Suppression)
app.MapMicrostructureKylesLambdaEndpoints();

// Horizons 281-285 Endpoint Mappings (Diamond Color Center Nanophotonics, Oligo Synthesis, Dual-Comb Spectroscopy, Merton Jump Diffusion & Perovskite Tandem)
app.MapMertonJumpDiffusionEndpoints();

// Horizons 286-290 Endpoint Mappings (Superconducting Crosstalk Cancellation, Single-Cell Ambient RNA Decontamination, Reaction Wheel Jitter, Bond Portfolio Immunization & Molten Salt CSP)
app.MapBondPortfolioImmunizationEndpoints();

// Horizons 291-295 Endpoint Mappings (Rare-Earth Optical Memory, HMW DNA Extraction, Lunar Orbit Insertion, CDO Copula Tranches & Solid Oxide Fuel Cells)
app.MapCdoCopulaTrancheEndpoints();

// Horizons 296-300 Endpoint Mappings (Silicon Spin Qubit Exchange, Nanopore Squiggle Basecalling, Aerobraking Drag, Almgren-Chriss Liquidation & Tokamak NTM ECCD Suppression)
app.MapAlmgrenChrissLiquidationEndpoints();

// Horizons 301-305 Endpoint Mappings (CV Cluster Teleportation, Cryo-ET Subtomogram Averaging, Solar CME Shock Arrival, FX LSV Barrier Options & PEM Electrolyzer Gas Crossover)
app.MapFxLsvBarrierEndpoints();

// Horizons 306-310 Endpoint Mappings (Superconducting Quantum Acoustics, GUIDE-Seq Prime Editing, Atmospheric Entry Guidance, TIPS Real Yield & DAC TVSA Sorbent)
app.MapTipsRealYieldEndpoints();

// Horizons 311-315 Endpoint Mappings (Majorana Zero Modes, Organoid Morphogenesis, OISL Crosslink PAT, CDS Index Tranche & Fischer-Tropsch SAF)
app.MapCdsIndexTrancheEndpoints();

// Horizons 316-320 Endpoint Mappings (Surface Code Lattice Surgery, Droplet Single-Cell Microfluidics, Spacecraft Charging, Cross-Currency Basis & VRFB Shunt Currents)
app.MapCrossCurrencyBasisEndpoints();

// Horizons 321-325 Endpoint Mappings (Dual-Species Tweezers, CODEX Proteomics, Spacecraft RCS Plume Impingement, SVI Volatility Surface & SOEC Steam Electrolysis)
app.MapSviVolatilityEndpoints();

// Horizons 326-330 Core ERP Suite (MRP II, AP 3-Way Match, Warehouse Bin Cycle Counting, Multi-Tier Payroll & COD Settlement)
app.MapMrpEndpoints();
app.MapApThreeWayMatchEndpoints();
app.MapWarehouseBinAuditEndpoints();
app.MapMultiTierPayrollEndpoints();
app.MapCodSettlementEndpoints();

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
