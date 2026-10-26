export enum PlanTier {
  Starter = 1,
  Growth = 2,
  Enterprise = 3
}

export interface FeatureEntitlementDto {
  id: string;
  tenantId: string;
  planTier: PlanTier;
  maxTeamSeats: number;
  monthlyIncludedAiTokens: number;
  monthlyIncludedVoiceMinutes: number;
  monthlyIncludedMessages: number;
  isCustomDomainEnabled: boolean;
  isSlaEscalationEnabled: boolean;
}

export interface TenantUsageSummaryDto {
  planTier: PlanTier;
  usedAiTokens: number;
  quotaAiTokens: number;
  usedVoiceMinutes: number;
  quotaVoiceMinutes: number;
  usedMessages: number;
  quotaMessages: number;
  tokenUsagePercent: number;
  voiceUsagePercent: number;
  messageUsagePercent: number;
}

export interface SubscriptionDetailsDto {
  tenantId: string;
  planTier: PlanTier;
  status: string;
  monthlyPrice: number;
  billingCycleStartUtc: string;
  billingCycleEndUtc: string;
  entitlements: FeatureEntitlementDto;
  usageSummary: TenantUsageSummaryDto;
}

export interface BillingInvoiceRecordDto {
  id: string;
  tenantId: string;
  invoiceNumber: string;
  amountDue: number;
  currency: string;
  status: string;
  stripeInvoiceId: string;
  hostedInvoiceUrl: string;
  createdAtUtc: string;
}
