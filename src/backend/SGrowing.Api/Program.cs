using Scalar.AspNetCore;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
using SGrowing.Api.Extensions;
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
using SGrowing.Api.Modules.Channels.WhatsApp.Companion;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Commerce.Affiliates;
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
using SGrowing.Api.Modules.Conversations.Presence;
using SGrowing.Api.Modules.Conversations.PresenceAndCsat;
using SGrowing.Api.Modules.Conversations.Sla;
using SGrowing.Api.Modules.CRM;
using SGrowing.Api.Modules.CRM.ContractSigning;
using SGrowing.Api.Modules.CRM.CustomFields;
using SGrowing.Api.Modules.CRM.Import;
using SGrowing.Api.Modules.Forms;
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
app.MapWorkflowGraphEndpoints();
app.MapCartReservationEndpoints();
app.MapEventFanoutEndpoints();
app.MapWhatsAppNewsletterEndpoints();
app.MapWhatsAppBroadcastPacingEndpoints();
app.MapWhatsAppCatalogCartEndpoints();
app.MapTelegramBotEndpoints();
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

// Real-Time SignalR Hub
app.MapHub<OmniHub>("/hubs/omni");

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
