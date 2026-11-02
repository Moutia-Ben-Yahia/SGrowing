using Scalar.AspNetCore;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
using SGrowing.Api.Extensions;
using SGrowing.Api.Hubs;
using SGrowing.Api.Modules.Agents;
using SGrowing.Api.Modules.Agents.Git;
using SGrowing.Api.Modules.Agents.Swarm;
using SGrowing.Api.Modules.Commerce.DynamicPricing;
using SGrowing.Api.Modules.Commerce.LiveStreaming;
using SGrowing.Api.Modules.Field.Sync;
using SGrowing.Api.Modules.Finance.Ledger;
using SGrowing.Api.Modules.SupplyChain.Replenishment;
using SGrowing.Api.Modules.DataArchitecture.EventSourcing;
using SGrowing.Api.Modules.Security.GraphFraud;
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
using SGrowing.Api.Modules.Energy.BessArbitrage;
using SGrowing.Api.Modules.Logistics.MaritimeEcoRouting;
using SGrowing.Api.Modules.Insurance.ParametricUnderwriting;
using SGrowing.Api.Modules.Manufacturing.OpticalQualityControl;
using SGrowing.Api.Modules.Security.MpcThresholdSigner;
using SGrowing.Api.Modules.Sustainability.CircularRemanufacturing;
using SGrowing.Api.Modules.Robotics.AgvFlockingCoordinator;
using SGrowing.Api.Modules.Finance.CreditRiskUnderwriting;
using SGrowing.Api.Modules.AI.DocumentGrounding;
using SGrowing.Api.Modules.Security.ZkIdentityVerification;
using SGrowing.Api.Modules.Aerospace.SatelliteOpticalDownlink;
using SGrowing.Api.Modules.Infrastructure.DePinWorkerArbiter;
using SGrowing.Api.Modules.Robotics.NeuromorphicOdometry;
using SGrowing.Api.Modules.Finance.BepsPillarTwo;
using SGrowing.Api.Modules.Governance.EnterpriseImmuneSystem;
using SGrowing.Api.Modules.Telecom.SubseaAcousticEarlyWarning;
using SGrowing.Api.Modules.Agriculture.PrecisionAgrigenomics;
using SGrowing.Api.Modules.Energy.HvdcGridStabilizer;
using SGrowing.Api.Modules.Finance.MicroPaymentMesh;
using SGrowing.Api.Modules.BioTech.MetabolicFluxOptimizer;
using SGrowing.Api.Modules.Aviation.AircraftTurnaroundOptimizer;
using SGrowing.Api.Modules.Logistics.RailHumpYardScheduler;
using SGrowing.Api.Modules.Energy.GeothermalMicroSeismicity;
using SGrowing.Api.Modules.Automotive.TeleoperationFoveatedStream;
using SGrowing.Api.Modules.Security.ConfidentialRemoteAttestation;
using SGrowing.Api.Modules.Sustainability.MineralCarbonation;
using SGrowing.Api.Modules.Finance.CbdcCrossBorderBridge;
using SGrowing.Api.Modules.Manufacturing.CleanroomAirflowPredictor;
using SGrowing.Api.Modules.Robotics.DroneUtmAirTraffic;
using SGrowing.Api.Modules.Pharma.HotMeltExtrusionPat;
using SGrowing.Api.Modules.Energy.SmrNeutronReactor;
using SGrowing.Api.Modules.Aerospace.ScramjetThermalProtection;
using SGrowing.Api.Modules.Security.QkdSatelliteArbiter;
using SGrowing.Api.Modules.Trade.RulesOfOriginTariff;
using SGrowing.Api.Modules.Security.ZkIrisBiometrics;
using SGrowing.Api.Modules.Energy.TokamakPlasmaMhd;
using SGrowing.Api.Modules.Healthcare.BciNeuralKinematics;
using SGrowing.Api.Modules.Aerospace.GravitationalInterferometerMesh;
using SGrowing.Api.Modules.Manufacturing.EuvLithographyPellicle;
using SGrowing.Api.Modules.Finance.HftOrderBookMicrostructure;
using SGrowing.Api.Modules.Marine.DeepSeaBuoyancyMesh;
using SGrowing.Api.Modules.Aerospace.CryoTurbopumpCavitation;
using SGrowing.Api.Modules.BioTech.CrisprCasCleavageKinetics;
using SGrowing.Api.Modules.Security.RingSignatureIdentity;
using SGrowing.Api.Modules.Energy.UhvCoronaIonization;
using SGrowing.Api.Modules.Energy.SolidStateBatteryEis;
using SGrowing.Api.Modules.Telecom.SubseaDwdmRamanCompensator;
using SGrowing.Api.Modules.Quantum.SuperconductingQubitGateArbiter;
using SGrowing.Api.Modules.Aerospace.SpacecraftAerocaptureGuidance;
using SGrowing.Api.Modules.Security.ThresholdMlDsaSigner;
using SGrowing.Api.Modules.Healthcare.MriMagnetQuenchArbiter;
using SGrowing.Api.Modules.Aerospace.SpaceElevatorTether;
using SGrowing.Api.Modules.Energy.PemElectrolyzerDegradation;
using SGrowing.Api.Modules.AI.SpikingRetinaGazeArbiter;
using SGrowing.Api.Modules.Finance.ZkStateRelayAggregator;
using SGrowing.Api.Modules.Security.CvQkdCoherentState;
using SGrowing.Api.Modules.Energy.MoltenSaltCspThermocline;
using SGrowing.Api.Modules.BioTech.MicrobialCrisprCascade;
using SGrowing.Api.Modules.Energy.FusionDivertorDetachment;
using SGrowing.Api.Modules.Aerospace.HypersonicWaveriderShock;
using SGrowing.Api.Modules.Energy.QuantumDotMegSolarCell;
using SGrowing.Api.Modules.Aerospace.HallThrusterPoleErosion;
using SGrowing.Api.Modules.BioTech.NanoporeTranslocationBasecaller;
using SGrowing.Api.Modules.Finance.ZkL3WatchtowerFraudProof;
using SGrowing.Api.Modules.Energy.SuperhotEgsMicroSeismicity;
using SGrowing.Api.Modules.Telecom.Terahertz6gBeamSquint;
using SGrowing.Api.Modules.Energy.CryoFusionPelletExtruder;
using SGrowing.Api.Modules.BioTech.CarTCellExhaustionKinetics;
using SGrowing.Api.Modules.Energy.DmfcRutheniumPoisoning;
using SGrowing.Api.Modules.Aerospace.RtgDecayThermocouple;
using SGrowing.Api.Modules.Aerospace.GravitationalMicrolensingExoplanet;
using SGrowing.Api.Modules.Energy.HtsRebcoCableDelamination;
using SGrowing.Api.Modules.BioTech.AntibodyAffinityMaturation;
using SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumTree;
using SGrowing.Api.Modules.Aerospace.HypersonicBltCrossflow;
using SGrowing.Api.Modules.Energy.SoecCoElectrolysis;
using SGrowing.Api.Modules.Aerospace.OislLaserPointingJitter;
using SGrowing.Api.Modules.BioTech.SpatialTranscriptomicsDeconvolution;
using SGrowing.Api.Modules.Finance.PqcCrossChainHtlc;
using SGrowing.Api.Modules.Energy.PlasmaRunawayElectronSuppression;
using SGrowing.Api.Modules.Environmental.QclPhotoacousticSpectroscopy;
using SGrowing.Api.Modules.Aerospace.ScramjetIsolatorShockTrain;
using SGrowing.Api.Modules.BioTech.CrisprPrimeEditingFlap;
using SGrowing.Api.Modules.Finance.AmmLossVersusRebalancing;
using SGrowing.Api.Modules.Energy.SupercriticalWellboreJouleThomson;
using SGrowing.Api.Modules.Energy.LiquidMetalBlanketHartmannFlow;
using SGrowing.Api.Modules.BioTech.MicrobialElectrosynthesis;
using SGrowing.Api.Modules.Aerospace.VasimrPlasmaPropulsion;
using SGrowing.Api.Modules.Finance.OrderFlowAuctionMev;
using SGrowing.Api.Modules.Manufacturing.HighNaEuvStochasticDefects;
using SGrowing.Api.Modules.Energy.Sco2BraytonCompressorSurge;
using SGrowing.Api.Modules.Telecom.DirectToCellDopplerTimingAdvance;
using SGrowing.Api.Modules.BioTech.CrisprCas12aCollateralCleavage;
using SGrowing.Api.Modules.Finance.ZkDarkPoolVdfBatching;
using SGrowing.Api.Modules.Aerospace.LunarRegolithMicrowaveSintering;
using SGrowing.Api.Modules.Energy.SmesCryogenicQuenchFastDump;
using SGrowing.Api.Modules.Defense.AdaptiveOpticsPhaseConjugation;
using SGrowing.Api.Modules.BioTech.CellFreeProteinTranslation;
using SGrowing.Api.Modules.Finance.DeFiLendingBadDebtSocialization;
using SGrowing.Api.Modules.Aerospace.HypersonicSkipGlideTrajectory;
using SGrowing.Api.Modules.Security.CvQkdExcessNoiseSecretKeyRate;
using SGrowing.Api.Modules.Environmental.DacSolidAmineChemisorption;
using SGrowing.Api.Modules.Aerospace.MartianAerocaptureGuidance;
using SGrowing.Api.Modules.Finance.HftVwapMicroPriceQueue;
using SGrowing.Api.Modules.Energy.LngBogReliquefactionExergy;
using SGrowing.Api.Modules.Energy.KilopowerSpaceNuclearBrayton;
using SGrowing.Api.Modules.AI.NeuromorphicStdpSynapticEnergy;
using SGrowing.Api.Modules.BioTech.ApiPolymorphContinuousCrystallization;
using SGrowing.Api.Modules.Finance.RestakingCryptoeconomicSlashing;
using SGrowing.Api.Modules.Energy.HempSubstationProtection;
using SGrowing.Api.Modules.Energy.DilutionRefrigeratorDemagnetization;
using SGrowing.Api.Modules.BioTech.TargetedAlphaEmitterDosimetry;
using SGrowing.Api.Modules.Aerospace.ObliqueDetonationWaveEngine;
using SGrowing.Api.Modules.Finance.CbdcAtomicDvpLiquiditySaving;
using SGrowing.Api.Modules.Energy.NeutrinoGeotomographyCoreDensity;
using SGrowing.Api.Modules.Manufacturing.MbeRheedGrowthKinetics;
using SGrowing.Api.Modules.BioTech.CrisprBaseEditorDeaminaseBystander;
using SGrowing.Api.Modules.Aerospace.HaleSolarEnergyBalance;
using SGrowing.Api.Modules.Security.ZkRecursiveFoldingScheme;
using SGrowing.Api.Modules.Energy.McfcCarbonCapturePolygeneration;
using SGrowing.Api.Modules.Transportation.MaglevInductrackNullFlux;
using SGrowing.Api.Modules.BioTech.OpticalCoherenceElastographyBiomechanics;
using SGrowing.Api.Modules.Marine.MhdMarineSilentPropulsion;
using SGrowing.Api.Modules.Finance.ZkRollupCensorshipResistance;
using SGrowing.Api.Modules.Energy.SupercriticalThermosiphonGeothermal;
using SGrowing.Api.Modules.BioTech.MicrofluidicLnpMrnaEncapsulation;
using SGrowing.Api.Modules.Environmental.QclAtmosphericPlumeInversion;
using SGrowing.Api.Modules.Aerospace.DualStageHallThrusterAnodeErosion;
using SGrowing.Api.Modules.Finance.ZkSolvencyMerkleSumAudit;
using SGrowing.Api.Modules.Energy.LiquidLithiumCpsDivertor;
using SGrowing.Api.Modules.Quantum.StrontiumOpticalLatticeClock;
using SGrowing.Api.Modules.BioTech.AcidMineDrainageBioleaching;
using SGrowing.Api.Modules.Aerospace.AerogelTpsAblationRecession;
using SGrowing.Api.Modules.Finance.LsdDepegArbitrageRedemption;
using SGrowing.Api.Modules.Transportation.HyperloopKantrowitzTubeAerodynamics;
using SGrowing.Api.Modules.Physics.StedSuperResolutionMicroscopy;
using SGrowing.Api.Modules.BioTech.PhageAssistedContinuousEvolution;
using SGrowing.Api.Modules.Aerospace.MiniMagnetosphericPlasmaPropulsion;
using SGrowing.Api.Modules.Finance.SovereignDebtClimateResilience;
using SGrowing.Api.Modules.Infrastructure.SeismicMetamaterialPhononicCloak;
using SGrowing.Api.Modules.Quantum.GaussianBosonSampling;
using SGrowing.Api.Modules.BioTech.EpigeneticMethylationClock;
using SGrowing.Api.Modules.Aerospace.ElectrodynamicTetherDeorbit;
using SGrowing.Api.Modules.Finance.VpinMarketMakerToxicFlow;
using SGrowing.Api.Modules.Energy.AdiabaticCaesThermalStorage;
using SGrowing.Api.Modules.Physics.ColdAtomGravimeterInterferometry;
using SGrowing.Api.Modules.BioTech.IvtMrnaCappingPolyATail;
using SGrowing.Api.Modules.Aerospace.AerogravityAssistTrajectory;
using SGrowing.Api.Modules.Finance.CrossAssetLiquiditySpillover;
using SGrowing.Api.Modules.Environmental.ScwoPfasElimination;
using SGrowing.Api.Modules.Quantum.NeutralAtomRydbergGates;
using SGrowing.Api.Modules.BioTech.CarTCellExhaustionReprogramming;
using SGrowing.Api.Modules.Aerospace.SolarSailRadiationPressure;
using SGrowing.Api.Modules.Finance.DynamicMarginCollateralVar;
using SGrowing.Api.Modules.Energy.MicrobialFuelCellBioenergy;
using SGrowing.Api.Modules.Quantum.NvCenterNanoscaleMagnetometry;
using SGrowing.Api.Modules.BioTech.PrimeEditingSearchAndReplace;
using SGrowing.Api.Modules.Aerospace.MhdBoundaryLayerFlowControl;
using SGrowing.Api.Modules.Finance.AmmDynamicImpermanentLossHedge;
using SGrowing.Api.Modules.Energy.DirectMethanolFuelCell;
using SGrowing.Api.Modules.Quantum.TopologicalMajoranaBraiding;
using SGrowing.Api.Modules.BioTech.CrisprRnpElectroporationKinetics;
using SGrowing.Api.Modules.Aerospace.NuclearThermalPropulsionPebbleBed;
using SGrowing.Api.Modules.Finance.CrossCurrencyFlashSwapArbitrage;
using SGrowing.Api.Modules.Energy.OceanThermalEnergyConversion;
using SGrowing.Api.Modules.Quantum.CavityOptomechanicsPhononSqueezing;
using SGrowing.Api.Modules.BioTech.AavCapsidDirectedEvolution;
using SGrowing.Api.Modules.Aerospace.RotatingDetonationRocketCombustor;
using SGrowing.Api.Modules.Finance.OrderFlowAdverseSelectionDrain;
using SGrowing.Api.Modules.Energy.AlkalineWaterElectrolysisDynamic;
using SGrowing.Api.Modules.Quantum.SuperconductingFluxoniumCoherence;
using SGrowing.Api.Modules.BioTech.CytidineBaseEditingBystanderMutagenesis;
using SGrowing.Api.Modules.Aerospace.HypersonicAerocaptureOrbitalInsertion;
using SGrowing.Api.Modules.Finance.StatisticalArbitrageOrnsteinUhlenbeck;
using SGrowing.Api.Modules.Energy.SolidOxideCoElectrolysisSyngas;
using SGrowing.Api.Modules.Quantum.NvCenterNuclearSpinRegister;
using SGrowing.Api.Modules.BioTech.LentiviralVectorIntegrationOncogenesis;
using SGrowing.Api.Modules.Aerospace.LunarCraterTetherRappelling;
using SGrowing.Api.Modules.Finance.SabrVolatilitySurfaceCalibration;
using SGrowing.Api.Modules.Energy.LfpBatteryThermalRunawaySuppression;
using SGrowing.Api.Modules.Quantum.BosonicCatQubitProtection;
using SGrowing.Api.Modules.BioTech.EngineeredExosomeCargoLoading;
using SGrowing.Api.Modules.Aerospace.AsteroidKineticImpactorDeflection;
using SGrowing.Api.Modules.Finance.HierarchicalRiskParityClustering;
using SGrowing.Api.Modules.Energy.VanadiumRedoxFlowBatteryBalancing;
using SGrowing.Api.Modules.AI;
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
app.MapLogisticsAndReverseCodEndpoints();
app.MapComplianceVaultEndpoints();

// Horizons 16-20 Endpoint Mappings
app.MapMultiAgentSwarmEndpoints();
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
app.MapBessArbitrageEndpoints();
app.MapMaritimeEcoRoutingEndpoints();
app.MapParametricInsuranceEndpoints();
app.MapOpticalQualityControlEndpoints();
app.MapMpcThresholdSignerEndpoints();

// Horizons 91-95 Endpoint Mappings
app.MapCircularRemanufacturingEndpoints();
app.MapAgvFlockingEndpoints();
app.MapCreditRiskUnderwritingEndpoints();
app.MapDocumentGroundingEndpoints();
app.MapZkIdentityVerificationEndpoints();

// Horizons 96-100 Endpoint Mappings (Century Capstone Wave)
app.MapSatelliteDownlinkEndpoints();
app.MapDePinEndpoints();
app.MapNeuromorphicOdometryEndpoints();
app.MapBepsPillarTwoEndpoints();
app.MapEnterpriseImmuneEndpoints();

// Horizons 101-105 Endpoint Mappings (Next-Era Wave)
app.MapSubseaAcousticEndpoints();
app.MapPrecisionAgrigenomicsEndpoints();
app.MapHvdcGridStabilizerEndpoints();
app.MapMicroPaymentMeshEndpoints();
app.MapMetabolicFluxEndpoints();

// Horizons 106-110 Endpoint Mappings (Deep Infrastructure Wave)
app.MapAircraftTurnaroundEndpoints();
app.MapRailHumpYardEndpoints();
app.MapGeothermalMicroSeismicityEndpoints();
app.MapTeleoperationFoveatedEndpoints();
app.MapConfidentialRemoteAttestationEndpoints();

// Horizons 111-115 Endpoint Mappings (Precision Decarbonization & Cyber-Physical Systems)
app.MapMineralCarbonationEndpoints();
app.MapCbdcCrossBorderEndpoints();
app.MapCleanroomAirflowEndpoints();
app.MapDroneUtmEndpoints();
app.MapHotMeltExtrusionEndpoints();

// Horizons 116-120 Endpoint Mappings (Nuclear SMR, Hypersonic Aerospace, QKD, Global Trade & ZK Biometrics)
app.MapSmrNeutronReactorEndpoints();
app.MapScramjetThermalProtectionEndpoints();
app.MapQkdSatelliteArbiterEndpoints();
app.MapRulesOfOriginTariffEndpoints();
app.MapZkIrisBiometricEndpoints();

// Horizons 121-125 Endpoint Mappings (Fusion Tokamak MHD, BCI Kinematics, Gravitational Interferometer, EUV Pellicle & HFT Microstructure)
app.MapTokamakPlasmaMhdEndpoints();
app.MapBciNeuralKinematicsEndpoints();
app.MapGravitationalInterferometerEndpoints();
app.MapEuvLithographyPellicleEndpoints();
app.MapHftOrderBookMicrostructureEndpoints();

// Horizons 126-130 Endpoint Mappings (Deep-Sea Submersible, Cryo Turbopump, CRISPR Cleavage, Ring Signatures & UHV Corona)
app.MapDeepSeaBuoyancyMeshEndpoints();
app.MapCryoTurbopumpCavitationEndpoints();
app.MapCrisprCasCleavageEndpoints();
app.MapRingSignatureIdentityEndpoints();
app.MapUhvCoronaIonizationEndpoints();

// Horizons 131-135 Endpoint Mappings (Solid-State Battery EIS, Subsea DWDM Raman, Superconducting Qubit Gates, Aerocapture Guidance & Threshold ML-DSA)
app.MapSolidStateBatteryEisEndpoints();
app.MapSubseaDwdmRamanCompensatorEndpoints();
app.MapSuperconductingQubitGateArbiterEndpoints();
app.MapSpacecraftAerocaptureGuidanceEndpoints();
app.MapThresholdMlDsaSignerEndpoints();

// Horizons 136-140 Endpoint Mappings (MRI Quench Arbiter, Space Elevator Tether, PEM Electrolyzer, Spiking Retina & ZK State Relay)
app.MapMriMagnetQuenchEndpoints();
app.MapSpaceElevatorTetherEndpoints();
app.MapPemElectrolyzerEndpoints();
app.MapSpikingRetinaEndpoints();
app.MapZkStateRelayEndpoints();

// Horizons 141-145 Endpoint Mappings (CV-QKD Coherent State, Molten Salt CSP Thermocline, Microbial CRISPR Cascade, Fusion Divertor Detachment & Hypersonic Waverider Shock)
app.MapCvQkdEndpoints();
app.MapMoltenSaltCspEndpoints();
app.MapMicrobialCrisprEndpoints();
app.MapFusionDivertorEndpoints();
app.MapHypersonicWaveriderEndpoints();

// Horizons 146-150 Endpoint Mappings (Quantum Dot MEG, Hall Thruster Pole Erosion, Nanopore Basecaller, ZK L3 Watchtower & Superhot EGS)
app.MapQuantumDotMegEndpoints();
app.MapHallThrusterEndpoints();
app.MapNanoporeEndpoints();
app.MapZkL3WatchtowerEndpoints();
app.MapSuperhotEgsEndpoints();

// Horizons 151-155 Endpoint Mappings (THz 6G Beam Squint, Cryo Fusion Pellets, CAR-T Exhaustion, DMFC Poisoning & Deep-Space RTG Decay)
app.MapTerahertz6gEndpoints();
app.MapCryoFusionPelletEndpoints();
app.MapCarTExhaustionEndpoints();
app.MapDmfcPoisoningEndpoints();
app.MapRtgDecayEndpoints();

// Horizons 156-160 Endpoint Mappings (Gravitational Microlensing Exoplanets, HTS REBCO Delamination, Antibody Affinity Maturation, ZK Solvency & Hypersonic BLT)
app.MapGravitationalMicrolensingEndpoints();
app.MapHtsRebcoCableEndpoints();
app.MapAntibodyAffinityEndpoints();
app.MapZkSolvencyEndpoints();
app.MapHypersonicBltEndpoints();

// Horizons 161-165 Endpoint Mappings (SOEC Co-Electrolysis, OISL Laser Jitter, Spatial Transcriptomics, PQC-HTLC & Tokamak Runaway Suppression)
app.MapSoecCoElectrolysisEndpoints();
app.MapOislLaserEndpoints();
app.MapSpatialTranscriptomicsEndpoints();
app.MapPqcHtlcEndpoints();
app.MapPlasmaRunawayEndpoints();

// Horizons 166-170 Endpoint Mappings (QCL Photoacoustic Spectroscopy, Scramjet Shock Train, CRISPR Prime Editing Flap, AMM LVR & Supercritical Wellbore)
app.MapQclPhotoacousticEndpoints();
app.MapScramjetIsolatorEndpoints();
app.MapPrimeEditingEndpoints();
app.MapAmmLvrEndpoints();
app.MapSupercriticalWellboreEndpoints();

// Horizons 171-175 Endpoint Mappings (Liquid Metal Blanket Hartmann Flow, Microbial Electrosynthesis, VASIMR Propulsion, OFA MEV & High-NA EUV)
app.MapLiquidMetalMhdEndpoints();
app.MapMicrobialElectrosynthesisEndpoints();
app.MapVasimrPlasmaEndpoints();
app.MapOrderFlowAuctionEndpoints();
app.MapHighNaEuvEndpoints();

// Horizons 176-180 Endpoint Mappings (sCO2 Brayton Compressor Surge, Direct-to-Cell LEO Doppler, CRISPR Cas12a DETECTR, ZK Dark Pool VDF & Lunar Regolith Microwave Sintering)
app.MapSco2BraytonEndpoints();
app.MapDirectToCellEndpoints();
app.MapCrisprCas12aEndpoints();
app.MapZkDarkPoolEndpoints();
app.MapLunarRegolithEndpoints();

// Horizons 181-185 Endpoint Mappings (SMES Quench Fast Dump, Adaptive Optics Phase Conjugation, Cell-Free Protein Translation, DeFi Bad Debt Socialization & Hypersonic Skip-Glide)
app.MapSmesQuenchEndpoints();
app.MapAdaptiveOpticsEndpoints();
app.MapCellFreeProteinEndpoints();
app.MapDeFiLendingEndpoints();
app.MapHypersonicSkipGlideEndpoints();

// Horizons 186-190 Endpoint Mappings (CV-QKD Excess Noise, DAC Solid-Amine Chemisorption, Martian Aerocapture Guidance, HFT Micro-Price & LNG BOG Re-Liquefaction)
app.MapCvQkdExcessNoiseEndpoints();
app.MapDacSolidAmineEndpoints();
app.MapMartianAerocaptureEndpoints();
app.MapHftMicroPriceEndpoints();
app.MapLngBogReliquefactionEndpoints();

// Horizons 191-195 Endpoint Mappings (Kilopower Space Brayton, Neuromorphic STDP, API Polymorph Continuous Crystallization, Re-Staking Slashing & HEMP Substation Protection)
app.MapKilopowerBraytonEndpoints();
app.MapNeuromorphicStdpEndpoints();
app.MapApiPolymorphEndpoints();
app.MapRestakingSlashingEndpoints();
app.MapHempSubstationEndpoints();

// Horizons 196-200 Endpoint Mappings (Double Century Milestone: Dilution Refrigerator ADR, Targeted Alpha TRT, Oblique Detonation Wave Engine, CBDC Atomic DvP & Neutrino Tomography)
app.MapDilutionRefrigeratorEndpoints();
app.MapTargetedAlphaDosimetryEndpoints();
app.MapObliqueDetonationEndpoints();
app.MapCbdcLiquiditySavingEndpoints();
app.MapNeutrinoGeotomographyEndpoints();

// Horizons 201-205 Endpoint Mappings (MBE RHEED Growth, CRISPR Base Editing Bystander, Stratospheric HALE Solar Balance, ZK Recursive Folding & MCFC Carbon Capture)
app.MapMbeRheedGrowthEndpoints();
app.MapCrisprBaseEditorEndpoints();
app.MapHaleSolarEnergyEndpoints();
app.MapZkRecursiveFoldingEndpoints();
app.MapMcfcCarbonCaptureEndpoints();

// Horizons 206-210 Endpoint Mappings (Maglev Inductrack EDS, Optical Coherence Elastography, Superconducting Marine MHD, ZK Rollup Censorship Resistance & Supercritical Thermosiphon Geothermal)
app.MapMaglevInductrackEndpoints();
app.MapOpticalElastographyEndpoints();
app.MapMhdMarinePropulsionEndpoints();
app.MapZkRollupCensorshipEndpoints();
app.MapSupercriticalThermosiphonEndpoints();

// Horizons 211-215 Endpoint Mappings (Microfluidic LNP mRNA, QCL Atmospheric Plume, Dual-Stage Hall Thruster, ZK Solvency Audit & Tokamak Liquid Lithium CPS Divertor)
app.MapMicrofluidicLnpEndpoints();
app.MapQclAtmosphericPlumeEndpoints();
app.MapDualStageHallThrusterEndpoints();
app.MapZkSolvencyAuditEndpoints();
app.MapLiquidLithiumDivertorEndpoints();

// Horizons 216-220 Endpoint Mappings (Strontium-87 Optical Clock, AMD Bioleaching, Aerogel TPS Ablation, LSD Depeg Arbitrage & Hyperloop Kantrowitz Aerodynamics)
app.MapStrontiumOpticalClockEndpoints();
app.MapAcidMineDrainageEndpoints();
app.MapAerogelTpsEndpoints();
app.MapLsdDepegArbitrageEndpoints();
app.MapHyperloopKantrowitzEndpoints();

// Horizons 221-225 Endpoint Mappings (STED Super-Resolution, PACE Continuous Evolution, M2P2 Plasma Propulsion, Sovereign Debt Climate Resilience & Seismic Metamaterial Cloak)
app.MapStedSuperResolutionEndpoints();
app.MapPaceEndpoints();
app.MapM2p2Endpoints();
app.MapSovereignDebtEndpoints();
app.MapSeismicCloakEndpoints();

// Horizons 226-230 Endpoint Mappings (Photonic GBS, Epigenetic Clock, Electrodynamic Tether, VPIN Toxic Flow & Adiabatic CAES)
app.MapGaussianBosonSamplingEndpoints();
app.MapEpigeneticClockEndpoints();
app.MapElectrodynamicTetherEndpoints();
app.MapVpinEndpoints();
app.MapAdiabaticCaesEndpoints();

// Horizons 231-235 Endpoint Mappings (Cold Atom Gravimeter, IVT mRNA Capping, Aerogravity-Assist, Cross-Asset Spillover & SCWO PFAS Elimination)
app.MapColdAtomGravimeterEndpoints();
app.MapIvtMrnaEndpoints();
app.MapAerogravityAssistEndpoints();
app.MapCrossAssetSpilloverEndpoints();
app.MapScwoEndpoints();

// Horizons 236-240 Endpoint Mappings (Neutral Atom Rydberg Gates, CAR-T Exhaustion Reprogramming, Solar Sail Radiation Pressure, Dynamic Margin Collateral VaR & Microbial Fuel Cell Bioenergy)
app.MapNeutralAtomRydbergEndpoints();
app.MapCarTCellExhaustionReprogrammingEndpoints();
app.MapSolarSailEndpoints();
app.MapDynamicMarginVarEndpoints();
app.MapMicrobialFuelCellEndpoints();

// Horizons 241-245 Endpoint Mappings (NV Center Magnetometry, Prime Editing Search-and-Replace, MHD Boundary Layer Flow Control, AMM Impermanent Loss Dynamic Hedge & Direct Methanol Fuel Cell)
app.MapNvCenterMagnetometryEndpoints();
app.MapPrimeEditingSearchAndReplaceEndpoints();
app.MapMhdBoundaryLayerEndpoints();
app.MapAmmDynamicHedgeEndpoints();
app.MapDmfcEndpoints();

// Horizons 246-250 Endpoint Mappings (Topological Majorana Braiding, CRISPR RNP Electroporation, Nuclear Thermal Propulsion, Cross-Currency Flash Swap Arbitrage & Ocean Thermal Energy Conversion)
app.MapTopologicalMajoranaEndpoints();
app.MapCrisprRnpElectroporationEndpoints();
app.MapNuclearThermalPropulsionEndpoints();
app.MapCrossCurrencyFlashSwapEndpoints();
app.MapOtecEndpoints();

// Horizons 251-255 Endpoint Mappings (Cavity Optomechanics Phonon Squeezing, AAV Capsid Evolution, Rotating Detonation Combustor, Order Flow Adverse Selection & Alkaline Water Electrolysis)
app.MapCavityOptomechanicsEndpoints();
app.MapAavCapsidEndpoints();
app.MapRotatingDetonationEndpoints();
app.MapOrderFlowAdverseSelectionEndpoints();
app.MapAlkalineWaterElectrolysisEndpoints();

// Horizons 256-260 Endpoint Mappings (Superconducting Fluxonium Coherence, Cytidine Base Editing Bystander, Hypersonic Aerocapture, Ornstein-Uhlenbeck Stat Arb & SOEC Co-Electrolysis)
app.MapSuperconductingFluxoniumEndpoints();
app.MapCytidineBaseEditingEndpoints();
app.MapHypersonicAerocaptureEndpoints();
app.MapStatisticalArbitrageEndpoints();
app.MapSolidOxideCoElectrolysisEndpoints();

// Horizons 261-265 Endpoint Mappings (NV Center Nuclear Spin Register, Lentiviral Integration Oncogenesis, Lunar Crater Tether, SABR Volatility Surface & LFP Battery Thermal Runaway)
app.MapNvCenterNuclearSpinEndpoints();
app.MapLentiviralIntegrationEndpoints();
app.MapLunarCraterTetherEndpoints();
app.MapSabrVolatilityEndpoints();
app.MapLfpBatteryThermalRunawayEndpoints();

// Horizons 266-270 Endpoint Mappings (Bosonic Cat Qubit Protection, Engineered Exosome Cargo, Asteroid Kinetic Impactor, Hierarchical Risk Parity & VRFB Spectrophotometry Balancing)
app.MapBosonicCatQubitEndpoints();
app.MapEngineeredExosomeEndpoints();
app.MapAsteroidKineticImpactorEndpoints();
app.MapHierarchicalRiskParityEndpoints();
app.MapVanadiumFlowBatteryEndpoints();

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
