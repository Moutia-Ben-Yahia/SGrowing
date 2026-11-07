/**
 * SGrowing Growth OS SDK Models & Types
 */

// --- AI Autonomous Missions ---

export type MissionStatus =
  | 'Draft'
  | 'Planning'
  | 'InProgress'
  | 'AwaitingApproval'
  | 'Completed'
  | 'Failed';

export type TaskExecutionStatus =
  | 'Pending'
  | 'Running'
  | 'WaitingApproval'
  | 'Succeeded'
  | 'Failed';

export interface MissionObjectiveRequest {
  title: string;
  strategicGoal: string;
  budgetLimit: number;
  targetGeo: string;
  constraints?: string[];
}

export interface MissionTaskNode {
  taskId: string;
  title: string;
  agentRole: string;
  dependsOnTaskIds: string[];
  status: TaskExecutionStatus;
  requiresHumanApproval: boolean;
  outputPayload?: string | null;
  failureReason?: string | null;
}

export interface AgentMissionPlan {
  missionId: string;
  title: string;
  strategicGoal: string;
  status: MissionStatus;
  taskDag: MissionTaskNode[];
  estimatedCost: number;
  createdAtUtc: string;
  completedAtUtc?: string | null;
}

export interface MissionApprovalDecisionRequest {
  approved: boolean;
  reviewerNotes?: string | null;
}

// --- Commerce Connectors & Orders ---

export type ConnectorAuthType =
  | 'OAuth2'
  | 'ApiKey'
  | 'WebhookSecret'
  | 'CustomToken';

export type SupportedPlatformTier =
  | 'RegionalMENA'
  | 'GlobalEnterprise'
  | 'OpenSource';

export interface ConnectorManifest {
  connectorId: string;
  name: string;
  description: string;
  category: string;
  iconUrl: string;
  documentationUrl: string;
  authType: ConnectorAuthType;
  tier: SupportedPlatformTier;
  supportedEntities: string[];
  webhookTopics: string[];
  apiVersion: string;
}

export interface CanonicalOrderItem {
  sku: string;
  title: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface CanonicalOrderPayload {
  externalOrderId: string;
  orderNumber: string;
  totalAmount: number;
  currency: string;
  customerEmail: string;
  customerPhone: string;
  shippingCity: string;
  shippingCountry: string;
  items: CanonicalOrderItem[];
  orderDateUtc: string;
  taxAmount?: number;
}

export interface UnifiedCommerceOrder {
  orderId: string;
  orderNumber: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string;
  shippingCity: string;
  shippingCountry: string;
  totalAmount: number;
  taxAmount: number;
  currency: string;
  paymentMethod: string;
  items: CanonicalOrderItem[];
  placedAtUtc: string;
  metadata?: Record<string, string>;
}

export interface TransformOrderRequest {
  rawJsonPayload: string;
  fieldMappings?: Record<string, string>;
}

export interface TransformPayloadResult {
  success: boolean;
  entityType: string;
  transformedOrder?: CanonicalOrderPayload | null;
  errorMessage?: string | null;
}

// --- Developer Webhooks & Logs ---

export interface WebhookSubscriptionRecord {
  id: string;
  tenantId: string;
  targetUrl: string;
  secretKey: string;
  subscribedEventsJson: string;
  isActive: boolean;
  failureCount: number;
  createdAtUtc: string;
}

export interface WebhookDeliveryAttemptLog {
  id: string;
  tenantId: string;
  subscriptionId: string;
  eventType: string;
  httpStatusCode: number;
  isSuccess: boolean;
  durationMs: number;
  failureReason?: string | null;
  attemptsCount: number;
  createdAtUtc: string;
}

export interface TestDispatchWebhookRequest {
  tenantId: string;
  eventType: string;
  payload: unknown;
}

export interface TestDispatchWebhookResponse {
  dispatchedCount: number;
  logs: WebhookDeliveryAttemptLog[];
}

// --- SRE Radar Vitals & Telemetry ---

export interface SreAlertDto {
  alertId: string;
  severity: string;
  serviceComponent: string;
  message: string;
  triggeredAtUtc: string;
}

export interface SreHealthRadarReport {
  overallHealthScore: number;
  overallStatus: string;
  webhookErrorBudgetRemainingPercent: number;
  p99WebhookDeliveryLatencyMs: number;
  clickstreamEventsPerSec: number;
  clickstreamPartitionLag: number;
  activeMissionsCount: number;
  deadLetterQueueDepth: number;
  activeAlerts: SreAlertDto[];
  timestampUtc: string;
}

export interface TraceContextPropagationHeaders {
  traceParent: string;
  traceState?: string | null;
  spanId: string;
  traceId: string;
}

export interface RecordSyntheticMetricRequest {
  metricType?: string;
  isSuccess?: boolean;
  durationMs?: number;
  eventCount?: number;
  agentRole?: string;
  tokenCount?: number;
}

// --- SDK Configuration Options ---

export interface SGrowingClientOptions {
  apiKey?: string;
  tenantId?: string;
  baseUrl?: string;
  timeoutMs?: number;
  fetch?: typeof globalThis.fetch;
  headers?: Record<string, string>;
}

export interface WebhookVerificationOptions {
  toleranceSeconds?: number;
  timestamp?: number | string;
}
