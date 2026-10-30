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
  { path: 'projects/tasks', component: ProjectTaskBoardComponent, canActivate: [authGuard] },
  { path: 'outbound/research', component: ProspectResearchCockpitComponent, canActivate: [authGuard] },
  { path: 'commerce/warehouses', component: MultiWarehouseCockpitComponent, canActivate: [authGuard] },
  { path: 'commerce/callcenter', component: CallCenterCockpitComponent, canActivate: [authGuard] },
  { path: 'organizations/margin', component: OrganizationMarginCockpitComponent, canActivate: [authGuard] },
  { path: 'workforce/timesheets', component: TimesheetLaborCockpitComponent, canActivate: [authGuard] },
  { path: 'channels/whatsapp-gateway', component: WhatsAppGatewayCockpitComponent, canActivate: [authGuard] },
  { path: 'channels/whatsapp-pairing', loadComponent: () => import('./features/channels/whatsapp-pairing-cockpit.component').then(m => m.WhatsAppPairingCockpitComponent), canActivate: [authGuard] },
  { path: 'funnels/autonomous', component: AutonomousFunnelCockpitComponent, canActivate: [authGuard] },
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
  { path: 'outbound/spintax', component: SpintaxEngineComponent, canActivate: [authGuard] },
  { path: 'deliverability/warmup-swarm', component: WarmupSwarmCockpitComponent, canActivate: [authGuard] },
  { path: 'deliverability/warmup-rescue', loadComponent: () => import('./features/deliverability/warmup-rescue-cockpit.component').then(m => m.WarmupRescueCockpitComponent), canActivate: [authGuard] },
  { path: 'inbox/omnichannel', component: OmnichannelCockpitComponent, canActivate: [authGuard] },
  { path: 'inbox/unified-desk', loadComponent: () => import('./features/inbox/omnichannel-unified-desk.component').then(m => m.OmnichannelUnifiedDeskComponent), canActivate: [authGuard] },
  { path: 'accounts/csv-import', component: CsvImportCockpitComponent, canActivate: [authGuard] },
  { path: 'chatbots/simulator', component: BotSimulatorRunnerComponent, canActivate: [authGuard] },
  { path: 'contracts/multi-party', component: MultipartySigningCockpitComponent, canActivate: [authGuard] },
  { path: 'courier/cod-fraud', component: CodLogisticsFraudCockpitComponent, canActivate: [authGuard] },
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
  { path: 'developer', component: DeveloperStudioComponent, canActivate: [authGuard] },
  { path: 'calendar', component: CalendarStudioComponent, canActivate: [authGuard] },
  { path: 'deliverability', component: DeliverabilityStudioComponent, canActivate: [authGuard] },
  { path: 'telemetry', component: TelemetryStudioComponent, canActivate: [authGuard] },
  { path: 'billing', component: BillingStudioComponent, canActivate: [authGuard] },
  { path: 'seo', component: SeoStudioComponent, canActivate: [authGuard] },
  { path: 'funnels', component: FunnelsStudioComponent, canActivate: [authGuard] },
  { path: 'operations', component: OperationsStudioComponent, canActivate: [authGuard] },
  { path: 'analytics', component: AnalyticsStudioComponent, canActivate: [authGuard] },
  { path: 'commerce/connectors', component: EcommerceConnectorsCockpitComponent, canActivate: [authGuard] },
  { path: 'commerce', component: CommerceStudioComponent, canActivate: [authGuard] },
  { path: 'outbound', component: OutboundStudioComponent, canActivate: [authGuard] },
  { path: 'broadcasts', component: BroadcastStudioComponent, canActivate: [authGuard] },
  { path: 'sla', component: SlaStudioComponent, canActivate: [authGuard] },
  { path: 'social/comment-to-dm', loadComponent: () => import('./features/social/social-comment-dm-studio.component').then(m => m.SocialCommentDmStudioComponent), canActivate: [authGuard] },
  { path: 'social', component: SocialStudioComponent, canActivate: [authGuard] },
  { path: 'voice', component: VoiceStudioComponent, canActivate: [authGuard] },
  { path: 'accounts', component: AccountsStudioComponent, canActivate: [authGuard] },
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
  { path: 'sales/cpq', loadComponent: () => import('./features/sales/cpq-quotation-studio.component').then(m => m.CpqQuotationStudioComponent), canActivate: [authGuard] },
  { path: 'integrations/webhook-mesh', loadComponent: () => import('./features/integrations/webhook-delivery-mesh-cockpit.component').then(m => m.WebhookDeliveryMeshCockpitComponent), canActivate: [authGuard] },
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
  { path: '**', redirectTo: '' }
];
