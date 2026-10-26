export interface TenantApiKeyDto {
  id: string;
  tenantId: string;
  name: string;
  keyPrefix: string;
  keyMasked: string;
  plainKey?: string;
  allowedScopesJson: string;
  rateLimitPerMinute: number;
  isRevoked: boolean;
  lastUsedAtUtc?: string;
  createdAtUtc: string;
}

export interface WebhookSubscriptionRecordDto {
  id: string;
  tenantId: string;
  targetUrl: string;
  secretKey: string;
  subscribedEventsJson: string;
  isActive: boolean;
  failureCount: number;
  createdAtUtc: string;
}

export interface WebhookDeliveryAttemptLogDto {
  id: string;
  tenantId: string;
  subscriptionId: string;
  eventType: string;
  payloadJson: string;
  responseStatusCode: number;
  isSuccess: boolean;
  executionDurationMs: number;
  attemptedAtUtc: string;
}
