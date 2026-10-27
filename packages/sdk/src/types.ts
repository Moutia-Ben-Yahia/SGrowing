export type PlanTier = 'Starter' | 'Growth' | 'Enterprise';

export type UsageMetricType =
  | 'AiTokens'
  | 'VoiceMinutes'
  | 'OutboundMessages'
  | 'TeamSeats'
  | 'ConnectedWarmupMailboxes'
  | 'MonthlyColdEmails'
  | 'CarrierWaybills'
  | 'StorageMb';

export interface SGrowingClientConfig {
  apiKey: string;
  tenantId: string;
  baseUrl?: string;
  timeoutMs?: number;
}

export interface Contact {
  id: string;
  tenantId: string;
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  companyName?: string;
  lifecycleStage: string;
  createdAtUtc: string;
}

export interface CreateContactParams {
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  companyName?: string;
}

export interface Deal {
  id: string;
  tenantId: string;
  contactId: string;
  title: string;
  amount: number;
  stage: string;
  currency: string;
  createdAtUtc: string;
}

export interface WebhookSubscription {
  id: string;
  tenantId: string;
  targetUrl: string;
  secretKey: string;
  subscribedEventsJson: string;
  isActive: boolean;
  failureCount: number;
  createdAtUtc: string;
}

export interface WebhookDeliveryLog {
  id: string;
  tenantId: string;
  subscriptionId: string;
  eventType: string;
  payloadJson: string;
  responseStatusCode: number;
  isSuccess: boolean;
  executionDurationMs: number;
  attemptNumber: number;
  maxRetries: number;
  isDeadLetter: boolean;
  deadLetterReason?: string;
  signatureHeader?: string;
  timestampHeader: number;
  attemptedAtUtc: string;
}

export interface TelemetryEvent {
  id: string;
  tenantId: string;
  sessionId: string;
  visitorId: string;
  eventName: string;
  url: string;
  referrer: string;
  deviceType: string;
  browser: string;
  countryCode: string;
  city: string;
  propertiesJson: string;
  timestampUtc: string;
}

export interface IngestTelemetryParams {
  sessionId: string;
  visitorId: string;
  eventName: string;
  url: string;
  referrer?: string;
  deviceType?: string;
  browser?: string;
  countryCode?: string;
  city?: string;
  properties?: Record<string, unknown>;
}

export interface PlanQuotaStatus {
  tenantId: string;
  planTier: PlanTier;
  planName: string;
  isHardCapEnforced: boolean;
  metrics: {
    metric: UsageMetricType;
    metricName: string;
    used: number;
    limit: number;
    usagePercentage: number;
    isExceeded: boolean;
  }[];
}

export interface AgentMission {
  id: string;
  tenantId: string;
  title: string;
  targetGoal: string;
  status: 'Pending' | 'Running' | 'PausedForApproval' | 'Completed' | 'Failed' | 'Canceled';
  autonomyMode: 'Auto-Approve' | 'Human-in-the-Loop' | 'Draft Only';
  totalBudgetUsd: number;
  spentBudgetUsd: number;
  tokensConsumed: number;
  currentStepIndex: number;
  reasoningTrace: string;
  createdAtUtc: string;
  steps: AgentMissionStep[];
}

export interface AgentMissionStep {
  id: string;
  missionId: string;
  stepIndex: number;
  toolName: string;
  inputArgumentsJson: string;
  outputResultJson: string;
  reasoningThought: string;
  status: 'Pending' | 'AwaitingApproval' | 'Executing' | 'Completed' | 'Failed' | 'Skipped';
  executionDurationMs: number;
  tokensConsumed: number;
  costUsd: number;
  approvalRequired: boolean;
  approvedBy?: string;
  executedAtUtc?: string;
}
