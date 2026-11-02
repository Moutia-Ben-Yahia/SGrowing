using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SGrowing.Api.Common;
using SGrowing.Api.Data;
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
using SGrowing.Api.Modules.Quantum.SuperconductingTwpaReadout;
using SGrowing.Api.Modules.BioTech.ProtacTernaryComplexKinetics;
using SGrowing.Api.Modules.Aerospace.HypersonicScramjetIsolatorShockTrain;
using SGrowing.Api.Modules.Finance.CrossCurrencyFxBasisSwapCip;
using SGrowing.Api.Modules.Energy.CompressedGasGeothermalEnergyStorage;
using SGrowing.Api.Modules.Quantum.NeutralAtomTweezerRearrangement;
using SGrowing.Api.Modules.BioTech.CellFreeProteinSynthesisExpression;
using SGrowing.Api.Modules.Aerospace.HallEffectThrusterErosion;
using SGrowing.Api.Modules.Finance.MicrostructureKylesLambdaAdverseSelection;
using SGrowing.Api.Modules.Energy.SolidStateBatteryDendriteSuppression;
using SGrowing.Api.Modules.Quantum.DiamondColorCenterNanophotonics;
using SGrowing.Api.Modules.BioTech.OligonucleotidePhosphoramiditeSynthesis;
using SGrowing.Api.Modules.Aerospace.DualCombSpectroscopyGreenhouseFlux;
using SGrowing.Api.Modules.Finance.MertonJumpDiffusionOptionPricing;
using SGrowing.Api.Modules.Energy.PerovskiteSiliconTandemRecombination;
using SGrowing.Api.Modules.Quantum.SuperconductingCrosstalkCancellation;
using SGrowing.Api.Modules.BioTech.SingleCellAmbientRnaDecontamination;
using SGrowing.Api.Modules.Aerospace.ReactionWheelMicroVibrationJitter;
using SGrowing.Api.Modules.Finance.BondPortfolioDurationConvexityImmunization;
using SGrowing.Api.Modules.Energy.MoltenSaltCspNitrateDecomposition;
using SGrowing.Api.Modules.Quantum.RareEarthOpticalMemory;
using SGrowing.Api.Modules.BioTech.HighMolecularWeightDnaExtraction;
using SGrowing.Api.Modules.Aerospace.LunarOrbitInsertionBallisticCapture;
using SGrowing.Api.Modules.Finance.CdoGaussianCopulaTrancheCorrelation;
using SGrowing.Api.Modules.Energy.SolidOxideFuelCellTriplePhaseBoundary;
using SGrowing.Api.Modules.Quantum.SiliconSpinQubitExchange;
using SGrowing.Api.Modules.BioTech.NanoporeSquiggleBasecalling;
using SGrowing.Api.Modules.Aerospace.AerobrakingAtmosphericDrag;
using SGrowing.Api.Modules.Finance.AlmgrenChrissOptimalLiquidation;
using SGrowing.Api.Modules.Energy.TokamakNtmEccdSuppression;
using SGrowing.Api.Modules.Quantum.ContinuousVariableClusterTeleportation;
using SGrowing.Api.Modules.BioTech.CryoEtSubtomogramAveraging;
using SGrowing.Api.Modules.Aerospace.SolarCmeShockArrival;
using SGrowing.Api.Modules.Finance.FxLocalStochasticVolatility;
using SGrowing.Api.Modules.Energy.PemElectrolyzerGasCrossover;
using SGrowing.Api.Modules.Quantum.SuperconductingQuantumAcoustics;
using SGrowing.Api.Modules.BioTech.GuideSeqPrimeEditingOffTarget;
using SGrowing.Api.Modules.Aerospace.AtmosphericEntryBlackoutGuidance;
using SGrowing.Api.Modules.Finance.TipsRealYieldBreakevenInflation;
using SGrowing.Api.Modules.Energy.DirectAirCaptureTvsaSorbent;
using SGrowing.Api.Modules.Quantum.MajoranaZeroModeBraiding;
using SGrowing.Api.Modules.BioTech.OrganoidLightSheetMorphogenesis;
using SGrowing.Api.Modules.Aerospace.OpticalInterSatelliteCrosslink;
using SGrowing.Api.Modules.Finance.CdsIndexTrancheCorrelation;
using SGrowing.Api.Modules.Energy.MicrochannelFischerTropschSaf;
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
using SGrowing.Api.Modules.Commerce.Fulfillment;
using SGrowing.Api.Modules.CRM.Pipeline;
using SGrowing.Api.Modules.Documents.Security;
using SGrowing.Api.Modules.Social.Listening;
using SGrowing.Api.Modules.Analytics.PredictiveLtv;
using SGrowing.Api.Modules.Voice.IVR;
using SGrowing.Api.Modules.Security.SyntheticData;
using SGrowing.Api.Modules.Search.Universal;
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
        services.AddSingleton<SGrowing.Core.Common.IConcurrencyLockService, SGrowing.Api.Common.Concurrency.RedisConcurrencyLockService>();
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
        services.AddSingleton<ILowLatencyAudioStreamingEngine, LowLatencyAudioStreamingEngine>();
        services.AddSingleton<IBargeInInterruptionManager, BargeInInterruptionManager>();
        services.AddSingleton<INeuralAnsweringMachineDetector, NeuralAnsweringMachineDetector>();
        services.AddSingleton<ILiveCallToolExecutor, LiveCallToolExecutor>();

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
        services.AddScoped<SGrowing.Api.Modules.Commerce.IReverseExchangeService, SGrowing.Api.Modules.Commerce.ReverseExchangeService>();
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
        services.AddSingleton<IAffiliateCommissionService, AffiliateCommissionService>();

        // Autonomous Multi-Carrier Logistics & Reverse COD Orchestration
        services.AddSingleton<ICodFraudPredictorService, CodFraudPredictorService>();
        services.AddSingleton<ICarrierSmartRouterService, CarrierSmartRouterService>();
        services.AddSingleton<SGrowing.Api.Modules.Commerce.Logistics.IReverseExchangeService, SGrowing.Api.Modules.Commerce.Logistics.ReverseExchangeService>();
        services.AddSingleton<ICourierRemittanceService, CourierRemittanceService>();

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

        // Phase 11: Autonomous Visual Workflow DAG Orchestrator & Temporal State Replay
        services.AddSingleton<IWorkflowEventStore, InMemoryWorkflowEventStore>();
        services.AddSingleton<IWorkflowExecutionStore, InMemoryWorkflowExecutionStore>();
        services.AddSingleton<IWorkflowApprovalTaskStore, InMemoryWorkflowApprovalTaskStore>();
        services.AddScoped<IWorkflowActionExecutor, DefaultWorkflowActionExecutor>();
        services.AddScoped<IAiPromptTransformer, DefaultAiPromptTransformer>();
        services.AddScoped<IDurableWorkflowExecutionEngine, DurableWorkflowExecutionEngine>();
        services.AddScoped<IHumanInTheLoopApprovalService, HumanInTheLoopApprovalService>();
        services.AddScoped<ILocalTimezonePacingService, LocalTimezonePacingService>();

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

        // Phase 15: SOC2 & GDPR Sovereign Compliance Vault
        services.AddSingleton<IMerkleComplianceAuditLedgerService, MerkleComplianceAuditLedgerService>();
        services.AddScoped<IGdprRightToBeForgottenCascadeService, GdprRightToBeForgottenCascadeService>();
        services.AddSingleton<IEnvelopeEncryptionService, EnvelopeEncryptionService>();
        services.AddScoped<IComplianceCertificateGeneratorService, ComplianceCertificateGeneratorService>();

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
        services.AddScoped<IProgrammaticSeoGeneratorService, ProgrammaticSeoGeneratorService>();
        services.AddScoped<ISchemaOrgJsonLdGeneratorService, SchemaOrgJsonLdGeneratorService>();
        services.AddScoped<ISerpPositionTrackerService, SerpPositionTrackerService>();
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

        // Phase 14: Autonomous Multi-Agent Swarm Defense, Semantic Vector Tool Caching & Prompt Firewalls
        services.AddSingleton<ICryptographicPromptCanaryService, CryptographicPromptCanaryService>();
        services.AddSingleton<ISemanticVectorToolCacheService, SemanticVectorToolCacheService>();
        services.AddSingleton<ICascadingModelRouterService, CascadingModelRouterService>();
        services.AddSingleton<IPiiSanitizationGatewayService, PiiSanitizationGatewayService>();

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
        services.AddSingleton<IEavDynamicSchemaValidator, EavDynamicSchemaValidator>();
        services.AddSingleton<IDynamicCustomObjectQueryBuilder, DynamicCustomObjectQueryBuilder>();
        services.AddSingleton<IDynamicCustomObjectService, DynamicCustomObjectService>();
        services.AddScoped<IWhatsAppNewsletterService, WhatsAppNewsletterService>();
        services.AddSingleton<IWhatsAppBroadcastPacingService, WhatsAppBroadcastPacingService>();
        services.AddSingleton<IWhatsAppCatalogCartService, WhatsAppCatalogCartService>();
        services.AddSingleton<ITelegramMarketingBotService, TelegramMarketingBotService>();

        // Phase 9: WhatsApp Baileys Multi-Device Cluster & Anti-Ban Message Pacing
        services.AddSingleton<IWhatsAppSessionClusterService, WhatsAppSessionClusterService>();
        services.AddSingleton<IAntiBanMessagePacingService, AntiBanMessagePacingService>();
        services.AddSingleton<IWhatsAppFlowsAndCatalogService, WhatsAppFlowsAndCatalogService>();
        services.AddScoped<ISmtpEmailSender, SmtpEmailSender>();
        services.AddScoped<IOmnichannelFailoverService, OmnichannelFailoverService>();

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
        services.AddWhatsAppCompanionServices();
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
        services.AddSingleton<IMultiPartySigningWorkflowService, MultiPartySigningWorkflowService>();
        services.AddSingleton<ICryptographicDocumentSealingService, CryptographicDocumentSealingService>();
        services.AddSingleton<IDocumentViewerTelemetryAndHeatmapService, DocumentViewerTelemetryAndHeatmapService>();
        services.AddSingleton<IDistributedEventMeshService, DistributedEventMeshService>();
        services.AddSingleton<IServerSideAttributionService, ServerSideAttributionService>();
        services.AddScoped<IDeterministicIdentityStitchingService, DeterministicIdentityStitchingService>();
        services.AddScoped<IMultiModelRevenueAttributionService, MultiModelRevenueAttributionService>();
        services.AddSingleton<IServerSideCapiFanoutService>(sp =>
            new ServerSideCapiFanoutService(
                sp.GetService<IHttpClientFactory>()?.CreateClient() ?? new HttpClient(),
                sp.GetService<ILogger<ServerSideCapiFanoutService>>()));
        services.AddSingleton<IFunnelSlippageMonitorService, FunnelSlippageMonitorService>();
        services.AddSingleton<IAppointmentSchedulingService, AppointmentSchedulingService>();
        services.AddSingleton<ICustomerLifecycleJourneyService, CustomerLifecycleJourneyService>();
        services.AddSingleton<IProductAnalyticsService, ProductAnalyticsService>();
        services.AddSingleton<IAgentPresenceCollisionService, AgentPresenceCollisionService>();
        services.AddSingleton<IHeadlessFormService, HeadlessFormService>();
        services.AddSingleton<IInAppMicroSurveyEngine, InAppMicroSurveyEngine>();
        services.AddSingleton<IProgressiveProfilingEngine, ProgressiveProfilingEngine>();
        services.AddSingleton<IConditionalBranchingEvaluator, ConditionalBranchingEvaluator>();
        services.AddSingleton<IFormSpamDefenseService, FormSpamDefenseService>();
        services.AddSingleton<IIncidentOnCallService, IncidentOnCallService>();
        services.AddSingleton<INotificationOrchestratorService, NotificationOrchestratorService>();
        services.AddSingleton<IConversationHierarchyService, ConversationHierarchyService>();
        services.AddSingleton<IWorkforceShiftService, WorkforceShiftService>();
        services.AddSingleton<IDocumentSecurityService, DocumentSecurityService>();
        services.AddSingleton<IDealSlippageRadarService, DealSlippageRadarService>();
        services.AddSingleton<ISocialListeningSentinelService, SocialListeningSentinelService>();
        services.AddSingleton<ISplitShipmentRoutingService, SplitShipmentRoutingService>();
        services.AddSingleton<IPredictiveLtvService, PredictiveLtvService>();
        services.AddSingleton<IVoiceIvrEngineService, VoiceIvrEngineService>();
        services.AddSingleton<ISyntheticDataGeneratorService, SyntheticDataGeneratorService>();
        services.AddSingleton<IUniversalSearchEngineService, UniversalSearchEngineService>();

        // Horizons 16-20 Services
        services.AddMultiAgentSwarmServices();
        services.AddDynamicElasticityPricingServices();
        services.AddDoubleEntryLedgerServices();
        services.AddCrdtDeltaSyncServices();
        services.AddLiveCommerceServices();

        // Horizons 21-25 Services
        services.AddSupplyChainReplenishmentServices();
        services.AddEventSourcingCqrsServices();
        services.AddIdentityGraphFraudServices();
        services.AddIdpServices();
        services.AddMicroFrontendServices();

        // Horizons 26-30 Services
        services.AddProcurementBiddingServices();
        services.AddZeroKnowledgeSolvencyServices();
        services.AddMultiRegionSovereignServices();
        services.AddAmrRoboticsBinPackingServices();
        services.AddPredictiveRetentionServices();

        // Horizons 31-35 Services
        services.AddCustomsServices();
        services.AddSessionReplayServices();
        services.AddCopywritingServices();
        services.AddVerifiableIdentityServices();
        services.AddChaosResilienceServices();

        // Horizons 36-40 Services
        services.AddFleetServices();
        services.AddConfidentialServices();
        services.AddVisualSearchServices();
        services.AddVoiceSynthesisServices();
        services.AddAutopilotServices();

        // Horizons 41-45 Services
        services.AddFactoringServices();
        services.AddColdChainServices();
        services.AddQualityGateServices();
        services.AddSupportSwarmServices();
        services.AddMultiCloudServices();

        // Horizons 46-50 Services
        services.AddEsgServices();
        services.AddPostQuantumServices();
        services.AddShrinkageServices();
        services.AddAdSpendServices();
        services.AddSovereignGovernanceServices();

        // Horizons 51-55 Services
        services.AddSatelliteCommsServices();
        services.AddNeuromorphicVisionServices();
        services.AddSettlementBridgeServices();
        services.AddCollaborativeCommerceServices();
        services.AddEdgeObservabilityServices();

        // Horizons 56-60 Services
        services.AddDigitalTwinServices();
        services.AddZkTraceabilityServices();
        services.AddRouteOptimizationServices();
        services.AddRetentionConciergeServices();
        services.AddConfidentialEnclaveServices();

        // Horizons 61-65 Services
        services.AddYieldManagementServices();
        services.AddKnowledgeHypergraphServices();
        services.AddDisruptionSimulationServices();
        services.AddVoiceLivenessServices();
        services.AddFinOpsArbitrageServices();

        // Horizons 66-70 Services
        services.AddCarbonExchangeServices();
        services.AddVerifiableCredentialsServices();
        services.AddPredictiveTelemetryServices();
        services.AddHsClassificationServices();
        services.AddServiceMeshServices();

        // Horizons 71-75 Services
        services.AddWarehouseSlottingServices();
        services.AddFxHedgingServices();
        services.AddDifferentialPrivacySandboxServices();
        services.AddDroneCorridorServices();
        services.AddZtnaServices();

        // Horizons 76-80 Services
        services.AddDataSovereigntyServices();
        services.AddDemandSensingServices();
        services.AddVoiceSentimentDeescalationServices();
        services.AddColdChainSlaServices();
        services.AddPostQuantumCryptoServices();

        // Horizons 81-85 Services
        services.AddDrayageSchedulerServices();
        services.AddHomomorphicAnalyticsServices();
        services.AddConcentratedLiquidityServices();
        services.AddProsodyDubbingServices();
        services.AddZkRollupBridgeServices();

        // Horizons 86-90 Services
        services.AddBessArbitrageServices();
        services.AddMaritimeEcoRoutingServices();
        services.AddParametricInsuranceServices();
        services.AddOpticalQualityControlServices();
        services.AddMpcThresholdSignerServices();

        // Horizons 91-95 Services
        services.AddCircularRemanufacturingServices();
        services.AddAgvFlockingServices();
        services.AddCreditRiskUnderwritingServices();
        services.AddDocumentGroundingServices();
        services.AddZkIdentityVerificationServices();

        // Horizons 96-100 Services (Century Capstone Wave)
        services.AddSatelliteDownlinkServices();
        services.AddDePinServices();
        services.AddNeuromorphicOdometryServices();
        services.AddBepsPillarTwoServices();
        services.AddEnterpriseImmuneServices();

        // Horizons 101-105 Services (Next-Era Wave)
        services.AddSubseaAcousticServices();
        services.AddPrecisionAgrigenomicsServices();
        services.AddHvdcGridStabilizerServices();
        services.AddMicroPaymentMeshServices();
        services.AddMetabolicFluxServices();

        // Horizons 106-110 Services (Deep Infrastructure Wave)
        services.AddAircraftTurnaroundServices();
        services.AddRailHumpYardServices();
        services.AddGeothermalMicroSeismicityServices();
        services.AddTeleoperationFoveatedServices();
        services.AddConfidentialRemoteAttestationServices();

        // Horizons 111-115 Services (Precision Decarbonization & Cyber-Physical Systems)
        services.AddMineralCarbonationServices();
        services.AddCbdcCrossBorderServices();
        services.AddCleanroomAirflowServices();
        services.AddDroneUtmServices();
        services.AddHotMeltExtrusionServices();

        // Horizons 116-120 Services (Nuclear SMR, Hypersonic Aerospace, QKD, Global Trade & ZK Biometrics)
        services.AddSmrNeutronReactorServices();
        services.AddScramjetThermalProtectionServices();
        services.AddQkdSatelliteArbiterServices();
        services.AddRulesOfOriginTariffServices();
        services.AddZkIrisBiometricServices();

        // Horizons 121-125 Services (Fusion Tokamak MHD, BCI Kinematics, Gravitational Interferometer, EUV Pellicle & HFT Microstructure)
        services.AddTokamakPlasmaMhdServices();
        services.AddBciNeuralKinematicsServices();
        services.AddGravitationalInterferometerServices();
        services.AddEuvLithographyPellicleServices();
        services.AddHftOrderBookMicrostructureServices();

        // Horizons 126-130 Services (Deep-Sea Submersible, Cryo Turbopump, CRISPR Cleavage, Ring Signatures & UHV Corona)
        services.AddDeepSeaBuoyancyMeshServices();
        services.AddCryoTurbopumpCavitationServices();
        services.AddCrisprCasCleavageServices();
        services.AddRingSignatureIdentityServices();
        services.AddUhvCoronaIonizationServices();

        // Horizons 131-135 Services (Solid-State Battery EIS, Subsea DWDM Raman, Superconducting Qubit Gates, Aerocapture Guidance & Threshold ML-DSA)
        services.AddSolidStateBatteryEisServices();
        services.AddSubseaDwdmRamanCompensatorServices();
        services.AddSuperconductingQubitGateArbiterServices();
        services.AddSpacecraftAerocaptureGuidanceServices();
        services.AddThresholdMlDsaSignerServices();

        // Horizons 136-140 Services (MRI Quench Arbiter, Space Elevator Tether, PEM Electrolyzer, Spiking Retina & ZK State Relay)
        services.AddMriMagnetQuenchServices();
        services.AddSpaceElevatorTetherServices();
        services.AddPemElectrolyzerDegradationServices();
        services.AddSpikingRetinaGazeArbiterServices();
        services.AddZkStateRelayAggregatorServices();

        // Horizons 141-145 Services (CV-QKD Coherent State, Molten Salt CSP Thermocline, Microbial CRISPR Cascade, Fusion Divertor Detachment & Hypersonic Waverider Shock)
        services.AddCvQkdCoherentStateServices();
        services.AddMoltenSaltCspThermoclineServices();
        services.AddMicrobialCrisprCascadeServices();
        services.AddFusionDivertorDetachmentServices();
        services.AddHypersonicWaveriderShockServices();

        // Horizons 146-150 Services (Quantum Dot MEG, Hall Thruster Pole Erosion, Nanopore Basecaller, ZK L3 Watchtower & Superhot EGS)
        services.AddQuantumDotMegSolarCellServices();
        services.AddHallThrusterPoleErosionServices();
        services.AddNanoporeTranslocationServices();
        services.AddZkL3WatchtowerFraudProofServices();
        services.AddSuperhotEgsMicroSeismicityServices();

        // Horizons 151-155 Services (THz 6G Beam Squint, Cryo Fusion Pellets, CAR-T Exhaustion, DMFC Poisoning & Deep-Space RTG Decay)
        services.AddTerahertz6gBeamSquintServices();
        services.AddCryoFusionPelletExtruderServices();
        services.AddCarTCellExhaustionServices();
        services.AddDmfcRutheniumPoisoningServices();
        services.AddRtgDecayThermocoupleServices();

        // Horizons 156-160 Services (Gravitational Microlensing Exoplanets, HTS REBCO Delamination, Antibody Affinity Maturation, ZK Solvency & Hypersonic BLT)
        services.AddGravitationalMicrolensingServices();
        services.AddHtsRebcoCableServices();
        services.AddAntibodyAffinityServices();
        services.AddZkSolvencyServices();
        services.AddHypersonicBltServices();

        // Horizons 161-165 Services (SOEC Co-Electrolysis, OISL Laser Jitter, Spatial Transcriptomics, PQC-HTLC & Tokamak Runaway Suppression)
        services.AddSoecCoElectrolysisServices();
        services.AddOislLaserServices();
        services.AddSpatialTranscriptomicsServices();
        services.AddPqcCrossChainHtlcServices();
        services.AddPlasmaRunawaySuppressionServices();

        // Horizons 166-170 Services (QCL Photoacoustic Spectroscopy, Scramjet Shock Train, CRISPR Prime Editing Flap, AMM LVR & Supercritical Wellbore)
        services.AddQclPhotoacousticServices();
        services.AddScramjetIsolatorServices();
        services.AddPrimeEditingServices();
        services.AddAmmLossVersusRebalancingServices();
        services.AddSupercriticalWellboreServices();

        // Horizons 171-175 Services (Liquid Metal Blanket Hartmann Flow, Microbial Electrosynthesis, VASIMR Propulsion, OFA MEV & High-NA EUV)
        services.AddLiquidMetalMhdServices();
        services.AddMicrobialElectrosynthesisServices();
        services.AddVasimrPlasmaPropulsionServices();
        services.AddOrderFlowAuctionServices();
        services.AddHighNaEuvServices();

        // Horizons 176-180 Services (sCO2 Brayton Compressor Surge, Direct-to-Cell LEO Doppler, CRISPR Cas12a DETECTR, ZK Dark Pool VDF & Lunar Regolith Microwave Sintering)
        services.AddSco2BraytonServices();
        services.AddDirectToCellServices();
        services.AddCrisprCas12aServices();
        services.AddZkDarkPoolServices();
        services.AddLunarRegolithServices();

        // Horizons 181-185 Services (SMES Quench Fast Dump, Adaptive Optics Phase Conjugation, Cell-Free Protein Translation, DeFi Bad Debt Socialization & Hypersonic Skip-Glide)
        services.AddSmesQuenchServices();
        services.AddAdaptiveOpticsServices();
        services.AddCellFreeProteinServices();
        services.AddDeFiLendingServices();
        services.AddHypersonicSkipGlideServices();

        // Horizons 186-190 Services (CV-QKD Excess Noise, DAC Solid-Amine Chemisorption, Martian Aerocapture Guidance, HFT Micro-Price & LNG BOG Re-Liquefaction)
        services.AddCvQkdExcessNoiseServices();
        services.AddDacSolidAmineServices();
        services.AddMartianAerocaptureServices();
        services.AddHftMicroPriceServices();
        services.AddLngBogReliquefactionServices();

        // Horizons 191-195 Services (Kilopower Space Brayton, Neuromorphic STDP, API Polymorph Continuous Crystallization, Re-Staking Slashing & HEMP Substation Protection)
        services.AddKilopowerBraytonServices();
        services.AddNeuromorphicStdpServices();
        services.AddApiPolymorphServices();
        services.AddRestakingSlashingServices();
        services.AddHempSubstationServices();

        // Horizons 196-200 Services (Double Century Milestone: Dilution Refrigerator ADR, Targeted Alpha TRT, Oblique Detonation Wave Engine, CBDC Atomic DvP & Neutrino Tomography)
        services.AddDilutionRefrigeratorServices();
        services.AddTargetedAlphaDosimetryServices();
        services.AddObliqueDetonationServices();
        services.AddCbdcLiquiditySavingServices();
        services.AddNeutrinoGeotomographyServices();

        // Horizons 201-205 Services (MBE RHEED Growth, CRISPR Base Editing Bystander, Stratospheric HALE Solar Balance, ZK Recursive Folding & MCFC Carbon Capture)
        services.AddMbeRheedGrowthServices();
        services.AddCrisprBaseEditorServices();
        services.AddHaleSolarEnergyServices();
        services.AddZkRecursiveFoldingServices();
        services.AddMcfcCarbonCaptureServices();

        // Horizons 206-210 Services (Maglev Inductrack EDS, Optical Coherence Elastography, Superconducting Marine MHD, ZK Rollup Censorship Resistance & Supercritical Thermosiphon Geothermal)
        services.AddMaglevInductrackServices();
        services.AddOpticalElastographyServices();
        services.AddMhdMarinePropulsionServices();
        services.AddZkRollupCensorshipServices();
        services.AddSupercriticalThermosiphonServices();

        // Horizons 211-215 Services (Microfluidic LNP mRNA, QCL Atmospheric Plume, Dual-Stage Hall Thruster, ZK Solvency Audit & Tokamak Liquid Lithium CPS Divertor)
        services.AddMicrofluidicLnpServices();
        services.AddQclAtmosphericPlumeServices();
        services.AddDualStageHallThrusterServices();
        services.AddZkSolvencyAuditServices();
        services.AddLiquidLithiumDivertorServices();

        // Horizons 216-220 Services (Strontium-87 Optical Clock, AMD Bioleaching, Aerogel TPS Ablation, LSD Depeg Arbitrage & Hyperloop Kantrowitz Aerodynamics)
        services.AddStrontiumOpticalLatticeClockServices();
        services.AddAcidMineDrainageBioleachingServices();
        services.AddAerogelTpsAblationRecessionServices();
        services.AddLsdDepegArbitrageServices();
        services.AddHyperloopKantrowitzAerodynamicsServices();

        // Horizons 221-225 Services (STED Super-Resolution, PACE Continuous Evolution, M2P2 Plasma Propulsion, Sovereign Debt Climate Resilience & Seismic Metamaterial Cloak)
        services.AddStedSuperResolutionServices();
        services.AddPaceServices();
        services.AddM2p2Services();
        services.AddSovereignDebtClimateResilienceServices();
        services.AddSeismicMetamaterialCloakServices();

        // Horizons 226-230 Services (Photonic GBS, Epigenetic Clock, Electrodynamic Tether, VPIN Toxic Flow & Adiabatic CAES)
        services.AddGaussianBosonSamplingServices();
        services.AddEpigeneticMethylationClockServices();
        services.AddElectrodynamicTetherDeorbitServices();
        services.AddVpinToxicFlowServices();
        services.AddAdiabaticCaesServices();

        // Horizons 231-235 Services (Cold Atom Gravimeter, IVT mRNA Capping, Aerogravity-Assist, Cross-Asset Spillover & SCWO PFAS Elimination)
        services.AddColdAtomGravimeterServices();
        services.AddIvtMrnaCappingPolyAServices();
        services.AddAerogravityAssistTrajectoryServices();
        services.AddCrossAssetLiquiditySpilloverServices();
        services.AddScwoPfasEliminationServices();

        // Horizons 236-240 Services (Neutral Atom Rydberg Gates, CAR-T Exhaustion Reprogramming, Solar Sail Radiation Pressure, Dynamic Margin Collateral VaR & Microbial Fuel Cell Bioenergy)
        services.AddNeutralAtomRydbergServices();
        services.AddCarTCellExhaustionReprogrammingServices();
        services.AddSolarSailRadiationPressureServices();
        services.AddDynamicMarginCollateralVarServices();
        services.AddMicrobialFuelCellBioenergyServices();

        // Horizons 241-245 Services (NV Center Magnetometry, Prime Editing Search-and-Replace, MHD Boundary Layer Flow Control, AMM Impermanent Loss Dynamic Hedge & Direct Methanol Fuel Cell)
        services.AddNvCenterMagnetometryServices();
        services.AddPrimeEditingSearchAndReplaceServices();
        services.AddMhdBoundaryLayerServices();
        services.AddAmmDynamicImpermanentLossHedgeServices();
        services.AddDirectMethanolFuelCellServices();

        // Horizons 246-250 Services (Topological Majorana Braiding, CRISPR RNP Electroporation, Nuclear Thermal Propulsion, Cross-Currency Flash Swap Arbitrage & Ocean Thermal Energy Conversion)
        services.AddTopologicalMajoranaBraidingServices();
        services.AddCrisprRnpElectroporationServices();
        services.AddNuclearThermalPropulsionPebbleBedServices();
        services.AddCrossCurrencyFlashSwapServices();
        services.AddOceanThermalEnergyConversionServices();

        // Horizons 251-255 Services (Cavity Optomechanics Phonon Squeezing, AAV Capsid Evolution, Rotating Detonation Combustor, Order Flow Adverse Selection & Alkaline Water Electrolysis)
        services.AddCavityOptomechanicsPhononSqueezingServices();
        services.AddAavCapsidDirectedEvolutionServices();
        services.AddRotatingDetonationRocketCombustorServices();
        services.AddOrderFlowAdverseSelectionDrainServices();
        services.AddAlkalineWaterElectrolysisDynamicServices();

        // Horizons 256-260 Services (Superconducting Fluxonium Coherence, Cytidine Base Editing Bystander, Hypersonic Aerocapture, Ornstein-Uhlenbeck Stat Arb & SOEC Co-Electrolysis)
        services.AddSuperconductingFluxoniumCoherenceServices();
        services.AddCytidineBaseEditingBystanderServices();
        services.AddHypersonicAerocaptureServices();
        services.AddStatisticalArbitrageOrnsteinUhlenbeckServices();
        services.AddSolidOxideCoElectrolysisServices();

        // Horizons 261-265 Services (NV Center Nuclear Spin Register, Lentiviral Integration Oncogenesis, Lunar Crater Tether, SABR Volatility Surface & LFP Battery Thermal Runaway)
        services.AddNvCenterNuclearSpinRegisterServices();
        services.AddLentiviralIntegrationServices();
        services.AddLunarCraterTetherRappellingServices();
        services.AddSabrVolatilitySurfaceCalibrationServices();
        services.AddLfpBatteryThermalRunawayServices();

        // Horizons 266-270 Services (Bosonic Cat Qubit Protection, Engineered Exosome Cargo, Asteroid Kinetic Impactor, Hierarchical Risk Parity & VRFB Spectrophotometry Balancing)
        services.AddBosonicCatQubitProtectionServices();
        services.AddEngineeredExosomeCargoLoadingServices();
        services.AddAsteroidKineticImpactorServices();
        services.AddHierarchicalRiskParityClusteringServices();
        services.AddVanadiumRedoxFlowBatteryBalancingServices();

        // Horizons 271-275 Services (TWPA Readout, PROTAC Kinetics, Scramjet Isolator, FX Basis CIP & Compressed Gas Geothermal)
        services.AddSuperconductingTwpaReadoutServices();
        services.AddProtacTernaryComplexServices();
        services.AddHypersonicScramjetIsolatorServices();
        services.AddCrossCurrencyFxBasisSwapCipServices();
        services.AddCompressedGasGeothermalServices();

        // Horizons 276-280 Services (Neutral Atom Tweezer Rearrangement, Cell-Free Protein Synthesis, Hall-Effect Thruster Erosion, Microstructure Kyle's Lambda & Solid-State Battery Dendrite Suppression)
        services.AddNeutralAtomTweezerRearrangementServices();
        services.AddCellFreeProteinSynthesisServices();
        services.AddHallEffectThrusterErosionServices();
        services.AddMicrostructureKylesLambdaServices();
        services.AddSolidStateBatteryDendriteServices();

        // Horizons 281-285 Services (Diamond Color Center Nanophotonics, Oligo Synthesis, Dual-Comb Spectroscopy, Merton Jump Diffusion & Perovskite Tandem)
        services.AddDiamondColorCenterNanophotonicsServices();
        services.AddOligoPhosphoramiditeSynthesisServices();
        services.AddDualCombSpectroscopyServices();
        services.AddMertonJumpDiffusionServices();
        services.AddPerovskiteSiliconTandemServices();

        // Horizons 286-290 Services (Superconducting Crosstalk Cancellation, Single-Cell Ambient RNA Decontamination, Reaction Wheel Jitter, Bond Portfolio Immunization & Molten Salt CSP)
        services.AddSuperconductingCrosstalkCancellationServices();
        services.AddSingleCellAmbientRnaServices();
        services.AddReactionWheelJitterServices();
        services.AddBondPortfolioImmunizationServices();
        services.AddMoltenSaltNitrateDecompositionServices();

        // Horizons 291-295 Services (Rare-Earth Optical Memory, HMW DNA Extraction, Lunar Orbit Insertion, CDO Copula Tranches & Solid Oxide Fuel Cells)
        services.AddRareEarthMemoryServices();
        services.AddHmwDnaExtractionServices();
        services.AddLunarOrbitInsertionServices();
        services.AddCdoCopulaTrancheServices();
        services.AddSolidOxideFuelCellServices();

        // Horizons 296-300 Services (Silicon Spin Qubit Exchange, Nanopore Squiggle Basecalling, Aerobraking Drag, Almgren-Chriss Liquidation & Tokamak NTM ECCD Suppression)
        services.AddSiliconSpinQubitServices();
        services.AddNanoporeSquiggleServices();
        services.AddAerobrakingDragServices();
        services.AddAlmgrenChrissLiquidationServices();
        services.AddTokamakNtmEccdServices();

        // Horizons 301-305 Services (CV Cluster Teleportation, Cryo-ET Subtomogram Averaging, Solar CME Shock Arrival, FX LSV Barrier Options & PEM Electrolyzer Gas Crossover)
        services.AddCvClusterTeleportationServices();
        services.AddCryoEtTomographyServices();
        services.AddSolarCmeShockServices();
        services.AddFxLsvBarrierServices();
        services.AddPemElectrolyzerGasCrossoverServices();

        // Horizons 306-310 Services (Superconducting Quantum Acoustics, GUIDE-Seq Prime Editing, Atmospheric Entry Guidance, TIPS Real Yield & DAC TVSA Sorbent)
        services.AddQuantumAcousticsServices();
        services.AddGuideSeqEditingServices();
        services.AddAtmosphericEntryServices();
        services.AddTipsRealYieldServices();
        services.AddDirectAirCaptureTvsaServices();

        // Horizons 311-315 Services (Majorana Zero Modes, Organoid Morphogenesis, OISL Crosslink PAT, CDS Index Tranche & Fischer-Tropsch SAF)
        services.AddMajoranaBraidingServices();
        services.AddOrganoidMorphogenesisServices();
        services.AddOislCrosslinkServices();
        services.AddCdsIndexTrancheServices();
        services.AddFischerTropschSafServices();

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
