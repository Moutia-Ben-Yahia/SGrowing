import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login.component';
import { authGuard } from './core/guards/auth.guard';
import { InboxShellComponent } from './features/inbox/inbox-shell.component';
import { OutboundStudioComponent } from './features/outbound/outbound-studio.component';
import { JourneyStudioComponent } from './features/journeys/journey-studio.component';
import { FeedbackStudioComponent } from './features/feedback/feedback-studio.component';
import { KnowledgeStudioComponent } from './features/knowledge/knowledge-studio.component';
import { BroadcastStudioComponent } from './features/broadcasts/broadcast-studio.component';
import { SlaStudioComponent } from './features/sla/sla-studio.component';
import { SocialStudioComponent } from './features/social/social-studio.component';
import { VoiceStudioComponent } from './features/voice/voice-studio.component';
import { AccountsStudioComponent } from './features/accounts/accounts-studio.component';
import { WorkflowStudioComponent } from './features/workflows/workflow-studio.component';
import { ContractsStudioComponent } from './features/contracts/contracts-studio.component';
import { RagStudioComponent } from './features/rag/rag-studio.component';
import { CommerceStudioComponent } from './features/commerce/commerce-studio.component';
import { AnalyticsStudioComponent } from './features/analytics/analytics-studio.component';
import { OperationsStudioComponent } from './features/operations/operations-studio.component';
import { SeoStudioComponent } from './features/seo/seo-studio.component';
import { FunnelsStudioComponent } from './features/funnels/funnels-studio.component';
import { CalendarStudioComponent } from './features/calendar/calendar-studio.component';
import { DeliverabilityStudioComponent } from './features/deliverability/deliverability-studio.component';
import { TelemetryStudioComponent } from './features/telemetry/telemetry-studio.component';
import { BillingStudioComponent } from './features/billing/billing-studio.component';
import { DigitalGoodsStudioComponent } from './features/digital-goods/digital-goods-studio.component';
import { FormsStudioComponent } from './features/forms/forms-studio.component';
import { HeadlessFormBuilderComponent } from './features/forms/headless-form-builder.component';
import { DeveloperStudioComponent } from './features/developer/developer-studio.component';
import { CourierCompanionComponent } from './features/courier/courier-companion.component';
import { CheckoutWidgetComponent } from './features/checkout/checkout-widget.component';
import { CreativeRadarStudioComponent } from './features/marketing/creative-radar-studio.component';
import { DocumentsStudioComponent } from './features/documents/documents-studio.component';
import { PublicationStudioComponent } from './features/publications/publication-studio.component';
import { ForecastingStudioComponent } from './features/forecasting/forecasting-studio.component';
import { RoadmapBoardComponent } from './features/roadmap/roadmap-board.component';
import { ChatbotCanvasComponent } from './features/funnels/chatbot-canvas.component';
import { WarmupDashboardComponent } from './features/deliverability/warmup-dashboard.component';
import { SoftphoneDialerComponent } from './features/outbound/softphone-dialer.component';
import { CpqBuilderComponent } from './features/commerce/cpq-builder.component';
import { SealingInspectorComponent } from './features/documents/sealing-inspector.component';
import { SpintaxEngineComponent } from './features/outbound/spintax-engine.component';
import { WarmupSwarmCockpitComponent } from './features/deliverability/warmup-swarm-cockpit.component';
import { OmnichannelCockpitComponent } from './features/inbox/omnichannel-cockpit.component';
import { CsvImportCockpitComponent } from './features/accounts/csv-import-cockpit.component';
import { BotSimulatorRunnerComponent } from './features/funnels/bot-simulator-runner.component';
import { MultipartySigningCockpitComponent } from './features/contracts/multiparty-signing-cockpit.component';
import { CodLogisticsFraudCockpitComponent } from './features/courier/cod-logistics-fraud-cockpit.component';
import { AgentConsoleComponent } from './features/agents/agent-console.component';
import { OnboardingWizardComponent } from './features/billing/onboarding-wizard.component';
import { AgentMissionCockpitComponent } from './features/agents/agent-mission-cockpit.component';
import { DeveloperWebhooksCockpitComponent } from './features/developer/developer-webhooks-cockpit.component';
import { FunnelAttributionCockpitComponent } from './features/analytics/funnel-attribution-cockpit.component';
import { EcommerceConnectorsCockpitComponent } from './features/commerce/ecommerce-connectors-cockpit.component';
import { ProjectTaskBoardComponent } from './features/projects/project-task-board.component';
import { ProspectResearchCockpitComponent } from './features/outbound/prospect-research-cockpit.component';
import { MultiWarehouseCockpitComponent } from './features/commerce/multi-warehouse-cockpit.component';
import { OrganizationMarginCockpitComponent } from './features/organizations/organization-margin-cockpit.component';
import { ClientOnboardingPortalComponent } from './features/projects/client-onboarding-portal.component';
import { TimesheetLaborCockpitComponent } from './features/workforce/timesheet-labor-cockpit.component';
import { WhatsAppGatewayCockpitComponent } from './features/channels/whatsapp-gateway-cockpit.component';
import { AutonomousFunnelCockpitComponent } from './features/funnels/autonomous-funnel-cockpit.component';
import { AgentFleetCockpitComponent } from './features/ai/agent-fleet-cockpit.component';
import { InvoiceManagementCockpitComponent } from './features/billing/invoice-management-cockpit.component';
import { AgencySnapshotCockpitComponent } from './features/accounts/agency-snapshot-cockpit.component';
import { CallCenterCockpitComponent } from './features/commerce/call-center-cockpit.component';
import { ProposalViewerComponent } from './features/documents/proposal-viewer.component';
import { VideoMeetingRoomComponent } from './features/calendar/video-meeting-room.component';
import { MultiInboxCockpitComponent } from './features/deliverability/multi-inbox-cockpit.component';
import { CarrierReconciliationCockpitComponent } from './features/commerce/carrier-reconciliation-cockpit.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', component: InboxShellComponent, canActivate: [authGuard] },
  { path: 'portal/onboarding/:token', component: ClientOnboardingPortalComponent },
  { path: 'p/:token', component: ProposalViewerComponent },
  { path: 'meet/:roomToken', component: VideoMeetingRoomComponent },
  { path: 'deliverability/multi-inbox', component: MultiInboxCockpitComponent, canActivate: [authGuard] },
  { path: 'commerce/reconciliation', component: CarrierReconciliationCockpitComponent, canActivate: [authGuard] },
  { path: 'commerce/b2b-wholesale', loadComponent: () => import('./features/commerce/b2b-wholesale-portal.component').then(m => m.B2BWholesalePortalComponent), canActivate: [authGuard] },
  { path: 'commerce/affiliates', loadComponent: () => import('./features/commerce/affiliate-commission-cockpit.component').then(m => m.AffiliateCommissionCockpitComponent), canActivate: [authGuard] },
  { path: 'projects/tasks', component: ProjectTaskBoardComponent, canActivate: [authGuard] },
  { path: 'outbound/research', component: ProspectResearchCockpitComponent, canActivate: [authGuard] },
  { path: 'commerce/warehouses', component: MultiWarehouseCockpitComponent, canActivate: [authGuard] },
  { path: 'commerce/callcenter', component: CallCenterCockpitComponent, canActivate: [authGuard] },
  { path: 'organizations/margin', component: OrganizationMarginCockpitComponent, canActivate: [authGuard] },
  { path: 'workforce/timesheets', component: TimesheetLaborCockpitComponent, canActivate: [authGuard] },
  { path: 'channels/whatsapp-gateway', component: WhatsAppGatewayCockpitComponent, canActivate: [authGuard] },
  { path: 'channels/whatsapp-pairing', loadComponent: () => import('./features/channels/whatsapp-pairing-cockpit.component').then(m => m.WhatsAppPairingCockpitComponent), canActivate: [authGuard] },
  { path: 'channels/whatsapp-pacing', loadComponent: () => import('./features/channels/whatsapp-broadcast-pacing-studio.component').then(m => m.WhatsAppBroadcastPacingStudioComponent), canActivate: [authGuard] },
  { path: 'channels/whatsapp-catalog', loadComponent: () => import('./features/channels/whatsapp-catalog-cart-studio.component').then(m => m.WhatsAppCatalogCartStudioComponent), canActivate: [authGuard] },
  { path: 'channels/telegram-bot', loadComponent: () => import('./features/channels/telegram-bot-studio.component').then(m => m.TelegramBotStudioComponent), canActivate: [authGuard] },
  { path: 'channels/whatsapp-companion', loadComponent: () => import('./features/channels/whatsapp-companion-studio.component').then(m => m.WhatsAppCompanionStudioComponent), canActivate: [authGuard] },
  { path: 'funnels/builder', loadComponent: () => import('./features/funnels/visual-funnel-builder.component').then(m => m.VisualFunnelBuilderComponent), canActivate: [authGuard] },
  { path: 'funnels/autonomous', component: AutonomousFunnelCockpitComponent, canActivate: [authGuard] },
  { path: 'funnels/split-testing', loadComponent: () => import('./features/funnels/funnel-split-testing-studio.component').then(m => m.FunnelSplitTestingStudioComponent), canActivate: [authGuard] },
  { path: 'funnels/canvas', loadComponent: () => import('./features/funnels/funnel-builder-canvas.component').then(m => m.FunnelBuilderCanvasComponent), canActivate: [authGuard] },
  { path: 'agents/workforce', loadComponent: () => import('./features/agents/agent-workforce-cockpit.component').then(m => m.AgentWorkforceCockpitComponent), canActivate: [authGuard] },
  { path: 'seo/crawler', loadComponent: () => import('./features/seo/seo-crawler-studio.component').then(m => m.SeoCrawlerStudioComponent), canActivate: [authGuard] },
  { path: 'accounts/snapshots', component: AgencySnapshotCockpitComponent, canActivate: [authGuard] },
  { path: 'ai/agent-fleet', component: AgentFleetCockpitComponent, canActivate: [authGuard] },
  { path: 'billing/invoices', component: InvoiceManagementCockpitComponent, canActivate: [authGuard] },
  { path: 'onboarding/tours', loadComponent: () => import('./features/onboarding/in-app-tour-studio.component').then(m => m.InAppTourStudioComponent), canActivate: [authGuard] },
  { path: 'onboarding/studio', loadComponent: () => import('./features/onboarding/in-app-tour-studio.component').then(m => m.InAppTourStudioComponent), canActivate: [authGuard] },
  { path: 'onboarding', component: OnboardingWizardComponent, canActivate: [authGuard] },
  { path: 'agents/missions', component: AgentMissionCockpitComponent, canActivate: [authGuard] },
  { path: 'developer/webhooks', component: DeveloperWebhooksCockpitComponent, canActivate: [authGuard] },
  { path: 'analytics/attribution', component: FunnelAttributionCockpitComponent, canActivate: [authGuard] },
  { path: 'outreach/warmup', loadComponent: () => import('./features/deliverability/mailbox-warmup-radar.component').then(m => m.MailboxWarmupRadarComponent), canActivate: [authGuard] },
  { path: 'outbound/spintax', component: SpintaxEngineComponent, canActivate: [authGuard] },
  { path: 'deliverability/warmup-swarm', component: WarmupSwarmCockpitComponent, canActivate: [authGuard] },
  { path: 'deliverability/warmup-rescue', loadComponent: () => import('./features/deliverability/warmup-rescue-cockpit.component').then(m => m.WarmupRescueCockpitComponent), canActivate: [authGuard] },
  { path: 'deliverability/custom-domains', loadComponent: () => import('./features/deliverability/custom-email-domain-studio.component').then(m => m.CustomEmailDomainStudioComponent), canActivate: [authGuard] },
  { path: 'inbox/omnichannel', component: OmnichannelCockpitComponent, canActivate: [authGuard] },
  { path: 'inbox/unified-desk', loadComponent: () => import('./features/inbox/omnichannel-unified-desk.component').then(m => m.OmnichannelUnifiedDeskComponent), canActivate: [authGuard] },
  { path: 'accounts/csv-import', component: CsvImportCockpitComponent, canActivate: [authGuard] },
  { path: 'chatbots/simulator', component: BotSimulatorRunnerComponent, canActivate: [authGuard] },
  { path: 'contracts/multi-party', component: MultipartySigningCockpitComponent, canActivate: [authGuard] },
  { path: 'contracts/e-sign', loadComponent: () => import('./features/contracts/contract-esign-studio.component').then(m => m.ContractEsignStudioComponent), canActivate: [authGuard] },
  { path: 'courier/cod-fraud', component: CodLogisticsFraudCockpitComponent, canActivate: [authGuard] },
  { path: 'courier/fleet-dispatch', loadComponent: () => import('./features/courier/geodetic-fleet-dispatch-cockpit.component').then(m => m.GeodeticFleetDispatchCockpitComponent), canActivate: [authGuard] },
  { path: 'agents/console', component: AgentConsoleComponent, canActivate: [authGuard] },
  { path: 'documents', component: DocumentsStudioComponent, canActivate: [authGuard] },
  { path: 'publications', component: PublicationStudioComponent, canActivate: [authGuard] },
  { path: 'forecasting', component: ForecastingStudioComponent, canActivate: [authGuard] },
  { path: 'roadmap', component: RoadmapBoardComponent, canActivate: [authGuard] },
  { path: 'courier', component: CourierCompanionComponent, canActivate: [authGuard] },
  { path: 'checkout', component: CheckoutWidgetComponent },
  { path: 'creatives', component: CreativeRadarStudioComponent, canActivate: [authGuard] },
  { path: 'digital-goods', component: DigitalGoodsStudioComponent, canActivate: [authGuard] },
  { path: 'forms', component: FormsStudioComponent, canActivate: [authGuard] },
  { path: 'forms/headless', component: HeadlessFormBuilderComponent, canActivate: [authGuard] },
  { path: 'developer', component: DeveloperStudioComponent, canActivate: [authGuard] },
  { path: 'calendar', component: CalendarStudioComponent, canActivate: [authGuard] },
  { path: 'deliverability', component: DeliverabilityStudioComponent, canActivate: [authGuard] },
  { path: 'telemetry', component: TelemetryStudioComponent, canActivate: [authGuard] },
  { path: 'billing/dunning', loadComponent: () => import('./features/billing/subscription-dunning-studio.component').then(m => m.SubscriptionDunningStudioComponent), canActivate: [authGuard] },
  { path: 'billing', component: BillingStudioComponent, canActivate: [authGuard] },
  { path: 'seo', component: SeoStudioComponent, canActivate: [authGuard] },
  { path: 'funnels', component: FunnelsStudioComponent, canActivate: [authGuard] },
  { path: 'operations', component: OperationsStudioComponent, canActivate: [authGuard] },
  { path: 'analytics/multi-touch', loadComponent: () => import('./features/analytics/multi-touch-attribution-cockpit.component').then(m => m.MultiTouchAttributionCockpitComponent), canActivate: [authGuard] },
  { path: 'analytics', component: AnalyticsStudioComponent, canActivate: [authGuard] },
  { path: 'commerce/connectors', component: EcommerceConnectorsCockpitComponent, canActivate: [authGuard] },
  { path: 'commerce', component: CommerceStudioComponent, canActivate: [authGuard] },
  { path: 'outbound', component: OutboundStudioComponent, canActivate: [authGuard] },
  { path: 'broadcasts', component: BroadcastStudioComponent, canActivate: [authGuard] },
  { path: 'sla', component: SlaStudioComponent, canActivate: [authGuard] },
  { path: 'sla/monitor', loadComponent: () => import('./features/sla/sla-breach-monitor-cockpit.component').then(m => m.SlaBreachMonitorCockpitComponent), canActivate: [authGuard] },
  { path: 'social/calendar', loadComponent: () => import('./features/social/social-content-calendar-studio.component').then(m => m.SocialContentCalendarStudioComponent), canActivate: [authGuard] },
  { path: 'social/comment-to-dm', loadComponent: () => import('./features/social/social-comment-dm-studio.component').then(m => m.SocialCommentDmStudioComponent), canActivate: [authGuard] },
  { path: 'social/publisher', loadComponent: () => import('./features/social/social-publisher-cockpit.component').then(m => m.SocialPublisherCockpitComponent), canActivate: [authGuard] },
  { path: 'social', component: SocialStudioComponent, canActivate: [authGuard] },
  { path: 'voice', component: VoiceStudioComponent, canActivate: [authGuard] },
  { path: 'voice/streaming-cockpit', loadComponent: () => import('./features/voice/voice-streaming-cockpit.component').then(m => m.VoiceStreamingCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/transcription', loadComponent: () => import('./features/voice/voice-transcription-studio.component').then(m => m.VoiceTranscriptionStudioComponent), canActivate: [authGuard] },
  { path: 'voice/diarization', redirectTo: 'voice/transcription', pathMatch: 'full' },
  { path: 'accounts', component: AccountsStudioComponent, canActivate: [authGuard] },
  { path: 'workflows/executor', loadComponent: () => import('./features/workflows/workflow-execution-cockpit.component').then(m => m.WorkflowExecutionCockpitComponent), canActivate: [authGuard] },
  { path: 'workflows', component: WorkflowStudioComponent, canActivate: [authGuard] },
  { path: 'contracts', component: ContractsStudioComponent, canActivate: [authGuard] },
  { path: 'rag', component: RagStudioComponent, canActivate: [authGuard] },
  { path: 'journeys/canvas', loadComponent: () => import('./features/journeys/journey-canvas-studio.component').then(m => m.JourneyCanvasStudioComponent), canActivate: [authGuard] },
  { path: 'journeys', component: JourneyStudioComponent, canActivate: [authGuard] },
  { path: 'feedback', component: FeedbackStudioComponent, canActivate: [authGuard] },
  { path: 'help', component: KnowledgeStudioComponent, canActivate: [authGuard] },
  { path: 'chatbots', component: ChatbotCanvasComponent, canActivate: [authGuard] },
  { path: 'warmup', component: WarmupDashboardComponent, canActivate: [authGuard] },
  { path: 'softphone', component: SoftphoneDialerComponent, canActivate: [authGuard] },
  { path: 'cpq', component: CpqBuilderComponent, canActivate: [authGuard] },
  { path: 'sealing', component: SealingInspectorComponent, canActivate: [authGuard] },
  { path: 'crm/customer-360', loadComponent: () => import('./features/crm/customer-360-cockpit.component').then(m => m.Customer360CockpitComponent), canActivate: [authGuard] },
  { path: 'crm/custom-objects', loadComponent: () => import('./features/crm/custom-objects-studio.component').then(m => m.CustomObjectsStudioComponent), canActivate: [authGuard] },
  { path: 'sales/cpq', loadComponent: () => import('./features/sales/cpq-quotation-studio.component').then(m => m.CpqQuotationStudioComponent), canActivate: [authGuard] },
  { path: 'integrations/webhook-mesh', loadComponent: () => import('./features/integrations/webhook-delivery-mesh-cockpit.component').then(m => m.WebhookDeliveryMeshCockpitComponent), canActivate: [authGuard] },
  { path: 'integrations/event-mesh', loadComponent: () => import('./features/integrations/distributed-event-mesh-cockpit.component').then(m => m.DistributedEventMeshCockpitComponent), canActivate: [authGuard] },
  { path: 'sales/predictive-cadence', loadComponent: () => import('./features/sales/predictive-cadence-studio.component').then(m => m.PredictiveCadenceStudioComponent), canActivate: [authGuard] },
  { path: 'commerce/inventory-replenishment', loadComponent: () => import('./features/commerce/inventory-replenishment-cockpit.component').then(m => m.InventoryReplenishmentCockpitComponent), canActivate: [authGuard] },
  { path: 'tenancy/whitelabel-branding', loadComponent: () => import('./features/tenancy/whitelabel-branding-studio.component').then(m => m.WhitelabelBrandingStudioComponent), canActivate: [authGuard] },
  { path: 'finance/cashflow-forecasting', loadComponent: () => import('./features/finance/cashflow-forecasting-studio.component').then(m => m.CashflowForecastingStudioComponent), canActivate: [authGuard] },
  { path: 'security/rate-limiting', loadComponent: () => import('./features/security/rate-limiting-cockpit.component').then(m => m.RateLimitingCockpitComponent), canActivate: [authGuard] },
  { path: 'crm/churn-retention', loadComponent: () => import('./features/crm/churn-retention-studio.component').then(m => m.ChurnRetentionStudioComponent), canActivate: [authGuard] },
  { path: 'security/compliance-audit-ledger', loadComponent: () => import('./features/security/compliance-audit-ledger.component').then(m => m.ComplianceAuditLedgerComponent), canActivate: [authGuard] },
  { path: 'feedback/board', loadComponent: () => import('./features/feedback/feedback-board-studio.component').then(m => m.FeedbackBoardStudioComponent), canActivate: [authGuard] },
  { path: 'billing/tax-invoicing', loadComponent: () => import('./features/billing/tax-invoice-ledger-studio.component').then(m => m.TaxInvoiceLedgerStudioComponent), canActivate: [authGuard] },
  { path: 'knowledge/rag-autopilot', loadComponent: () => import('./features/knowledge/hybrid-rag-autopilot-studio.component').then(m => m.HybridRagAutopilotStudioComponent), canActivate: [authGuard] },
  { path: 'documents/telemetry', loadComponent: () => import('./features/documents/document-telemetry-studio.component').then(m => m.DocumentTelemetryStudioComponent), canActivate: [authGuard] },
  { path: 'commerce/dynamic-pricing', loadComponent: () => import('./features/commerce/dynamic-pricing-studio.component').then(m => m.DynamicPricingStudioComponent), canActivate: [authGuard] },
  { path: 'crm/lead-enrichment', loadComponent: () => import('./features/crm/lead-enrichment-studio.component').then(m => m.LeadEnrichmentStudioComponent), canActivate: [authGuard] },
  { path: 'security/idempotency-ledger', loadComponent: () => import('./features/security/idempotency-ledger-cockpit.component').then(m => m.IdempotencyLedgerCockpitComponent), canActivate: [authGuard] },
  { path: 'security/continuous-compliance', loadComponent: () => import('./features/security/continuous-compliance-studio.component').then(m => m.ContinuousComplianceStudioComponent), canActivate: [authGuard] },
  { path: 'scheduling/appointments', loadComponent: () => import('./features/scheduling/appointment-scheduling-studio.component').then(m => m.AppointmentSchedulingStudioComponent), canActivate: [authGuard] },
  { path: 'attribution/capi-cockpit', loadComponent: () => import('./features/attribution/server-side-attribution-cockpit.component').then(m => m.ServerSideAttributionCockpitComponent), canActivate: [authGuard] },
  { path: 'telemetry/analytics', loadComponent: () => import('./features/telemetry/product-analytics-cockpit.component').then(m => m.ProductAnalyticsCockpitComponent), canActivate: [authGuard] },
  { path: 'lifecycle/journeys', loadComponent: () => import('./features/lifecycle/customer-lifecycle-studio.component').then(m => m.CustomerLifecycleStudioComponent), canActivate: [authGuard] },
  { path: 'conversations/agent-presence', loadComponent: () => import('./features/conversations/agent-presence-cockpit.component').then(m => m.AgentPresenceCockpitComponent), canActivate: [authGuard] },
  { path: 'conversations/incidents', loadComponent: () => import('./features/conversations/incident-matrix-studio.component').then(m => m.IncidentMatrixStudioComponent), canActivate: [authGuard] },
  { path: 'notifications/orchestrator', loadComponent: () => import('./features/notifications/notification-orchestrator-studio.component').then(m => m.NotificationOrchestratorStudioComponent), canActivate: [authGuard] },
  { path: 'conversations/hierarchy', loadComponent: () => import('./features/conversations/conversation-hierarchy-studio.component').then(m => m.ConversationHierarchyStudioComponent), canActivate: [authGuard] },
  { path: 'workforce/shifts', loadComponent: () => import('./features/workforce/shift-scheduling-studio.component').then(m => m.ShiftSchedulingStudioComponent), canActivate: [authGuard] },
  { path: 'documents/security', loadComponent: () => import('./features/documents/document-security-studio.component').then(m => m.DocumentSecurityStudioComponent), canActivate: [authGuard] },
  { path: 'crm/deal-slippage', loadComponent: () => import('./features/crm/deal-slippage-studio.component').then(m => m.DealSlippageStudioComponent), canActivate: [authGuard] },
  { path: 'social/listening', loadComponent: () => import('./features/social/social-listening-studio.component').then(m => m.SocialListeningStudioComponent), canActivate: [authGuard] },
  { path: 'commerce/fulfillment-routing', loadComponent: () => import('./features/commerce/split-shipment-studio.component').then(m => m.SplitShipmentStudioComponent), canActivate: [authGuard] },
  { path: 'analytics/pltv', loadComponent: () => import('./features/analytics/pltv-analytics-studio.component').then(m => m.PltvAnalyticsStudioComponent), canActivate: [authGuard] },
  { path: 'voice/ivr-flow', loadComponent: () => import('./features/voice/voice-ivr-studio.component').then(m => m.VoiceIvrStudioComponent), canActivate: [authGuard] },
  { path: 'security/synthetic-data', loadComponent: () => import('./features/security/synthetic-data-studio.component').then(m => m.SyntheticDataStudioComponent), canActivate: [authGuard] },
  { path: 'search/universal', loadComponent: () => import('./features/search/universal-search-cockpit.component').then(m => m.UniversalSearchCockpitComponent), canActivate: [authGuard] },
  { path: 'crm/identity-graph', loadComponent: () => import('./features/crm/identity-graph-cockpit.component').then(m => m.IdentityGraphCockpitComponent), canActivate: [authGuard] },
  { path: 'marketing/ad-capi-roas', loadComponent: () => import('./features/marketing/ad-capi-roas-cockpit.component').then(m => m.AdCapiRoasCockpitComponent), canActivate: [authGuard] },
  { path: 'workflows/dag-telemetry', loadComponent: () => import('./features/workflows/dag-telemetry-studio.component').then(m => m.DagTelemetryStudioComponent), canActivate: [authGuard] },
  { path: 'agents/growth-director', loadComponent: () => import('./features/agents/growth-director-mission-cockpit.component').then(m => m.GrowthDirectorMissionCockpitComponent), canActivate: [authGuard] },
  { path: 'developer/sandbox', loadComponent: () => import('./features/developer/developer-sandbox-cockpit.component').then(m => m.DeveloperSandboxCockpitComponent), canActivate: [authGuard] },
  { path: 'sales/proposals', loadComponent: () => import('./features/sales/b2b-proposal-cpq-cockpit.component').then(m => m.B2BProposalCpqCockpitComponent), canActivate: [authGuard] },
  { path: 'conversations/unified', loadComponent: () => import('./features/conversations/unified-support-inbox.component').then(m => m.UnifiedSupportInboxComponent), canActivate: [authGuard] },
  { path: 'ai/governance', loadComponent: () => import('./features/ai/ai-governance-cockpit.component').then(m => m.AiGovernanceCockpitComponent), canActivate: [authGuard] },
  { path: 'ai/swarm', loadComponent: () => import('./features/ai/swarm-orchestration-cockpit.component').then(m => m.SwarmOrchestrationCockpitComponent), canActivate: [authGuard] },
  { path: 'integrations/marketplace', loadComponent: () => import('./features/integrations/app-marketplace-cockpit.component').then(m => m.AppMarketplaceCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/webrtc-sdr', loadComponent: () => import('./features/voice/webrtc-sdr-cockpit.component').then(m => m.WebRtcSdrCockpitComponent), canActivate: [authGuard] },
  { path: 'analytics/executive', loadComponent: () => import('./features/analytics/executive-revenue-cockpit.component').then(m => m.ExecutiveRevenueCockpitComponent), canActivate: [authGuard] },
  { path: 'tenancy/licensing', loadComponent: () => import('./features/tenancy/enterprise-license-cockpit.component').then(m => m.EnterpriseLicenseCockpitComponent), canActivate: [authGuard] },
  { path: 'seo/geo-visibility', loadComponent: () => import('./features/seo/geo-visibility-cockpit.component').then(m => m.GeoVisibilityCockpitComponent), canActivate: [authGuard] },
  { path: 'outreach/whatsapp-broadcast', loadComponent: () => import('./features/outreach/whatsapp-broadcast-cockpit.component').then(m => m.WhatsAppBroadcastCockpitComponent), canActivate: [authGuard] },
  { path: 'ai/knowledge-rag', loadComponent: () => import('./features/ai/semantic-rag-cockpit.component').then(m => m.SemanticRagCockpitComponent), canActivate: [authGuard] },
  { path: 'seo/programmatic', loadComponent: () => import('./features/seo/programmatic-seo-cockpit.component').then(m => m.ProgrammaticSeoCockpitComponent), canActivate: [authGuard] },
  { path: 'billing/tax-compliance', loadComponent: () => import('./features/billing/tax-compliance-cockpit.component').then(m => m.TaxComplianceCockpitComponent), canActivate: [authGuard] },
  { path: 'sales/deal-copilot', loadComponent: () => import('./features/sales/deal-copilot-cockpit.component').then(m => m.DealCopilotCockpitComponent), canActivate: [authGuard] },
  { path: 'cms/headless-studio', loadComponent: () => import('./features/cms/headless-cms-cockpit.component').then(m => m.HeadlessCmsCockpitComponent), canActivate: [authGuard] },
  { path: 'growth/affiliates', loadComponent: () => import('./features/growth/affiliate-loyalty-cockpit.component').then(m => m.AffiliateLoyaltyCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/call-copilot', loadComponent: () => import('./features/voice/live-call-copilot-cockpit.component').then(m => m.LiveCallCopilotCockpitComponent), canActivate: [authGuard] },
  { path: 'sales/abm-territories', loadComponent: () => import('./features/sales/abm-territory-cockpit.component').then(m => m.AbmTerritoryCockpitComponent), canActivate: [authGuard] },
  { path: 'security/compliance-vault', loadComponent: () => import('./features/security/compliance-vault-cockpit.component').then(m => m.ComplianceVaultCockpitComponent), canActivate: [authGuard] },
  { path: 'lifecycle/journey-orchestrator', loadComponent: () => import('./features/lifecycle/journey-orchestrator-cockpit.component').then(m => m.JourneyOrchestratorCockpitComponent), canActivate: [authGuard] },
  { path: 'funnels/canvas-builder', loadComponent: () => import('./features/funnels/funnel-canvas-builder.component').then(m => m.FunnelCanvasBuilderComponent), canActivate: [authGuard] },
  { path: 'commerce/fulfillment-matrix', loadComponent: () => import('./features/commerce/fulfillment-matrix-cockpit.component').then(m => m.FulfillmentMatrixCockpitComponent), canActivate: [authGuard] },
  { path: 'analytics/bi-report-builder', loadComponent: () => import('./features/analytics/bi-report-builder.component').then(m => m.BiReportBuilderComponent), canActivate: [authGuard] },
  { path: 'developer/webhook-mesh', loadComponent: () => import('./features/developer/webhook-mesh-cockpit.component').then(m => m.WebhookMeshCockpitComponent), canActivate: [authGuard] },
  { path: 'ai/knowledge-graph', loadComponent: () => import('./features/ai/knowledge-graph-cockpit.component').then(m => m.KnowledgeGraphCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/ivr-canvas', loadComponent: () => import('./features/voice/voice-ivr-tree-cockpit.component').then(m => m.VoiceIvrTreeCockpitComponent), canActivate: [authGuard] },
  { path: 'commerce/fraud-radar', loadComponent: () => import('./features/commerce/fraud-firewall-cockpit.component').then(m => m.FraudFirewallCockpitComponent), canActivate: [authGuard] },
  { path: 'security/collaboration-audit', loadComponent: () => import('./features/security/collaboration-audit-cockpit.component').then(m => m.CollaborationAuditCockpitComponent), canActivate: [authGuard] },
  { path: 'developer/app-builder', loadComponent: () => import('./features/developer/app-generator-cockpit.component').then(m => m.AppGeneratorCockpitComponent), canActivate: [authGuard] },
  { path: 'tenancy/custom-domains', loadComponent: () => import('./features/tenancy/custom-domains-cockpit.component').then(m => m.CustomDomainsCockpitComponent), canActivate: [authGuard] },
  { path: 'ai/video-avatar', loadComponent: () => import('./features/ai/video-avatar-cockpit.component').then(m => m.VideoAvatarCockpitComponent), canActivate: [authGuard] },
  { path: 'commerce/replenishment', loadComponent: () => import('./features/commerce/replenishment-cockpit.component').then(m => m.ReplenishmentCockpitComponent), canActivate: [authGuard] },
  { path: 'social/auto-triage', loadComponent: () => import('./features/social/social-triage-cockpit.component').then(m => m.SocialTriageCockpitComponent), canActivate: [authGuard] },
  { path: 'contracts/churn-rescue', loadComponent: () => import('./features/contracts/churn-rescue-cockpit.component').then(m => m.ChurnRescueCockpitComponent), canActivate: [authGuard] },
  { path: 'growth/influencers', loadComponent: () => import('./features/growth/influencer-escrow-cockpit.component').then(m => m.InfluencerEscrowCockpitComponent), canActivate: [authGuard] },
  { path: 'advertising/spend-autopilot', loadComponent: () => import('./features/advertising/ad-spend-autopilot-cockpit.component').then(m => m.AdSpendAutopilotCockpitComponent), canActivate: [authGuard] },
  { path: 'analytics/data-clean-room', loadComponent: () => import('./features/analytics/data-clean-room-cockpit.component').then(m => m.DataCleanRoomCockpitComponent), canActivate: [authGuard] },
  { path: 'channels/whatsapp-flows', loadComponent: () => import('./features/channels/flow-interactive-cockpit.component').then(m => m.FlowInteractiveCockpitComponent), canActivate: [authGuard] },
  { path: 'infrastructure/self-healing', loadComponent: () => import('./features/infrastructure/self-healing-cockpit.component').then(m => m.SelfHealingMeshCockpitComponent), canActivate: [authGuard] },
  { path: 'intelligence/de-anonymization', loadComponent: () => import('./features/intelligence/de-anonymization-cockpit.component').then(m => m.DeAnonymizationCockpitComponent), canActivate: [authGuard] },
  { path: 'advertising/audience-sync', loadComponent: () => import('./features/advertising/audience-sync-cockpit.component').then(m => m.AudienceSyncCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/objection-scoring', loadComponent: () => import('./features/voice/objection-scoring-cockpit.component').then(m => m.ObjectionScoringCockpitComponent), canActivate: [authGuard] },
  { path: 'seo/topical-authority', loadComponent: () => import('./features/seo/topical-authority-cockpit.component').then(m => m.TopicalAuthorityCockpitComponent), canActivate: [authGuard] },
  { path: 'infrastructure/feature-flags', loadComponent: () => import('./features/infrastructure/feature-flags-cockpit.component').then(m => m.FeatureFlagsCockpitComponent), canActivate: [authGuard] },
  { path: 'intelligence/waterfall-enrichment', loadComponent: () => import('./features/intelligence/waterfall-enrichment-cockpit.component').then(m => m.WaterfallEnrichmentCockpitComponent), canActivate: [authGuard] },
  { path: 'content/social-proof', loadComponent: () => import('./features/content/social-proof-cockpit.component').then(m => m.SocialProofCockpitComponent), canActivate: [authGuard] },
  { path: 'commerce/cart-rescue', loadComponent: () => import('./features/commerce/cart-rescue-cockpit.component').then(m => m.CartRescueCockpitComponent), canActivate: [authGuard] },
  { path: 'partnerships/deal-registration', loadComponent: () => import('./features/partnerships/deal-registration-cockpit.component').then(m => m.DealRegistrationCockpitComponent), canActivate: [authGuard] },
  { path: 'infrastructure/semantic-cache', loadComponent: () => import('./features/infrastructure/semantic-cache-cockpit.component').then(m => m.SemanticCacheCockpitComponent), canActivate: [authGuard] },
  { path: 'intelligence/buying-committee', loadComponent: () => import('./features/intelligence/buying-committee-cockpit.component').then(m => m.BuyingCommitteeCockpitComponent), canActivate: [authGuard] },
  { path: 'advertising/creative-bandit', loadComponent: () => import('./features/advertising/creative-bandit-cockpit.component').then(m => m.CreativeBanditCockpitComponent), canActivate: [authGuard] },
  { path: 'commerce/cpq-margin', loadComponent: () => import('./features/commerce/cpq-margin-cockpit.component').then(m => m.CpqMarginCockpitComponent), canActivate: [authGuard] },
  { path: 'support/localization', loadComponent: () => import('./features/support/localization-cockpit.component').then(m => m.LocalizationCockpitComponent), canActivate: [authGuard] },
  { path: 'infrastructure/disaster-recovery', loadComponent: () => import('./features/infrastructure/disaster-recovery-cockpit.component').then(m => m.DisasterRecoveryCockpitComponent), canActivate: [authGuard] },
  { path: 'content/repurposing', loadComponent: () => import('./features/content/content-repurposing-cockpit.component').then(m => m.ContentRepurposingCockpitComponent), canActivate: [authGuard] },
  { path: 'analytics/attribution-shapley', loadComponent: () => import('./features/analytics/attribution-shapley-cockpit.component').then(m => m.AttributionShapleyCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/agent-assist', loadComponent: () => import('./features/voice/agent-assist-cockpit.component').then(m => m.AgentAssistCockpitComponent), canActivate: [authGuard] },
  { path: 'commerce/tariffs', loadComponent: () => import('./features/commerce/tariff-calculator-cockpit.component').then(m => m.TariffCalculatorCockpitComponent), canActivate: [authGuard] },
  { path: 'security/ephemeral-vault', loadComponent: () => import('./features/security/ephemeral-vault-cockpit.component').then(m => m.EphemeralVaultCockpitComponent), canActivate: [authGuard] },
  { path: 'intelligence/win-loss', loadComponent: () => import('./features/intelligence/win-loss-cockpit.component').then(m => m.WinLossCockpitComponent), canActivate: [authGuard] },
  { path: 'advertising/retargeting-matrix', loadComponent: () => import('./features/advertising/retargeting-matrix-cockpit.component').then(m => m.RetargetingMatrixCockpitComponent), canActivate: [authGuard] },
  { path: 'support/sentiment-deescalation', loadComponent: () => import('./features/support/sentiment-deescalation-cockpit.component').then(m => m.SentimentDeescalationCockpitComponent), canActivate: [authGuard] },
  { path: 'billing/consumption-metering', loadComponent: () => import('./features/billing/consumption-metering-cockpit.component').then(m => m.ConsumptionMeteringCockpitComponent), canActivate: [authGuard] },
  { path: 'infrastructure/shard-mesh', loadComponent: () => import('./features/infrastructure/shard-mesh-cockpit.component').then(m => m.ShardMeshCockpitComponent), canActivate: [authGuard] },
  { path: 'intelligence/mutual-action-plan', loadComponent: () => import('./features/intelligence/mutual-action-plan-cockpit.component').then(m => m.MutualActionPlanCockpitComponent), canActivate: [authGuard] },
  { path: 'advertising/catalog-feeds', loadComponent: () => import('./features/advertising/catalog-feeds-cockpit.component').then(m => m.CatalogFeedsCockpitComponent), canActivate: [authGuard] },
  { path: 'support/capacity-planning', loadComponent: () => import('./features/support/capacity-planning-cockpit.component').then(m => m.CapacityPlanningCockpitComponent), canActivate: [authGuard] },
  { path: 'finance/fx-hedging', loadComponent: () => import('./features/finance/fx-hedging-cockpit.component').then(m => m.FxHedgingCockpitComponent), canActivate: [authGuard] },
  { path: 'security/micro-segmentation', loadComponent: () => import('./features/security/micro-segmentation-cockpit.component').then(m => m.MicroSegmentationCockpitComponent), canActivate: [authGuard] },
  { path: 'intelligence/account-expansion', loadComponent: () => import('./features/intelligence/account-expansion-cockpit.component').then(m => m.AccountExpansionCockpitComponent), canActivate: [authGuard] },
  { path: 'commerce/influencer-escrow-v2', loadComponent: () => import('./features/commerce/influencer-escrow-v2-cockpit.component').then(m => m.InfluencerEscrowV2CockpitComponent), canActivate: [authGuard] },
  { path: 'voice/call-transfer', loadComponent: () => import('./features/voice/call-transfer-cockpit.component').then(m => m.CallTransferCockpitComponent), canActivate: [authGuard] },
  { path: 'finance/asc606-revenue', loadComponent: () => import('./features/finance/asc606-revenue-cockpit.component').then(m => m.Asc606RevenueCockpitComponent), canActivate: [authGuard] },
  { path: 'security/secret-vault-mesh', loadComponent: () => import('./features/security/secret-vault-mesh-cockpit.component').then(m => m.SecretVaultMeshCockpitComponent), canActivate: [authGuard] },
  { path: 'intelligence/pipeline-velocity', loadComponent: () => import('./features/intelligence/pipeline-velocity-cockpit.component').then(m => m.PipelineVelocityCockpitComponent), canActivate: [authGuard] },
  { path: 'advertising/bid-pacing', loadComponent: () => import('./features/advertising/bid-pacing-cockpit.component').then(m => m.BidPacingCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/call-compliance', loadComponent: () => import('./features/voice/call-compliance-cockpit.component').then(m => m.CallComplianceCockpitComponent), canActivate: [authGuard] },
  { path: 'finance/tax-nexus', loadComponent: () => import('./features/finance/tax-nexus-cockpit.component').then(m => m.TaxNexusCockpitComponent), canActivate: [authGuard] },
  { path: 'infrastructure/chaos-mesh', loadComponent: () => import('./features/infrastructure/chaos-mesh-cockpit.component').then(m => m.ChaosMeshCockpitComponent), canActivate: [authGuard] },
  { path: 'commerce/cpq-approval', loadComponent: () => import('./features/commerce/cpq-approval-cockpit.component').then(m => m.CpqApprovalCockpitComponent), canActivate: [authGuard] },
  { path: 'marketing/shapley-attribution', loadComponent: () => import('./features/marketing/shapley-attribution-cockpit.component').then(m => m.ShapleyAttributionCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/pitch-tuning', loadComponent: () => import('./features/voice/pitch-tuning-cockpit.component').then(m => m.PitchTuningCockpitComponent), canActivate: [authGuard] },
  { path: 'finance/treasury-yield', loadComponent: () => import('./features/finance/treasury-yield-cockpit.component').then(m => m.TreasuryYieldCockpitComponent), canActivate: [authGuard] },
  { path: 'infrastructure/edge-mesh', loadComponent: () => import('./features/infrastructure/edge-mesh-cockpit.component').then(m => m.EdgeMeshCockpitComponent), canActivate: [authGuard] },
  { path: 'commerce/commission-settlement', loadComponent: () => import('./features/commerce/commission-settlement-cockpit.component').then(m => m.CommissionSettlementCockpitComponent), canActivate: [authGuard] },
  { path: 'advertising/retail-media-auction', loadComponent: () => import('./features/advertising/retail-media-auction-cockpit.component').then(m => m.RetailMediaAuctionCockpitComponent), canActivate: [authGuard] },
  { path: 'voice/audio-noise-cancellation', loadComponent: () => import('./features/voice/audio-noise-cancellation-cockpit.component').then(m => m.AudioNoiseCancellationCockpitComponent), canActivate: [authGuard] },
  { path: 'finance/vendor-bill-matching', loadComponent: () => import('./features/finance/vendor-bill-matching-cockpit.component').then(m => m.VendorBillMatchingCockpitComponent), canActivate: [authGuard] },
  { path: 'infrastructure/k8s-isolation-mesh', loadComponent: () => import('./features/infrastructure/k8s-isolation-mesh-cockpit.component').then(m => m.K8sIsolationMeshCockpitComponent), canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];
