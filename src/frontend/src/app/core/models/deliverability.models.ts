export interface DomainDnsHealthRecordDto {
  id: string;
  tenantId: string;
  domainName: string;
  hasSpf: boolean;
  spfRecord: string;
  hasDkim: boolean;
  dkimSelector: string;
  hasDmarc: boolean;
  dmarcPolicy: string;
  hasMx: boolean;
  mxHost: string;
  overallHealthScore: number;
  issuesJson: string;
  lastCheckedAtUtc: string;
}

export interface EmailSpamAnalysisReportDto {
  id: string;
  tenantId: string;
  subject: string;
  bodyHtmlPreview: string;
  spamScore: number;
  triggerWordsDetectedJson: string;
  hasUnsubscribeHeader: boolean;
  imageToTextRatio: number;
  verdict: string;
  createdAtUtc: string;
}

export interface MailboxWarmupCadenceDto {
  id: string;
  tenantId: string;
  senderEmail: string;
  dailyTargetLimit: number;
  currentDayNumber: number;
  warmupRampCurve: string;
  totalSimulatedSent: number;
  totalSimulatedReceived: number;
  spamRescueCount: number;
  isActive: boolean;
  startedAtUtc: string;
}

export interface WarmupExchangeLogDto {
  id: string;
  tenantId: string;
  warmupCadenceId: string;
  peerInboxEmail: string;
  direction: string;
  timestampUtc: string;
}

export type MailboxProviderType = 'GoogleWorkspace' | 'Microsoft365' | 'CustomSmtp' | 1 | 2 | 3;
export type InboxRotationStatusType = 'Active' | 'Warmup' | 'Quarantined' | 'Cooling' | 1 | 2 | 3 | 4;

export interface MultiInboxAccountDto {
  id: string;
  tenantId: string;
  email: string;
  displayName: string;
  provider: MailboxProviderType;
  dailyQuota: number;
  sentTodayCount: number;
  remainingDailyQuota: number;
  warmupEnabled: boolean;
  warmupRampDailyIncrement: number;
  healthScore: number;
  isActive: boolean;
  status: InboxRotationStatusType;
  quarantineReason?: string | null;
  quarantinedAtUtc?: string | null;
  thawedAtUtc?: string | null;
  lastDispatchedAtUtc?: string | null;
  totalDispatched: number;
  totalDelivered: number;
  totalBounced: number;
  totalReplied: number;
  totalOpened: number;
  bounceRate: number;
  replyRate: number;
  openRate: number;
  hasSpf: boolean;
  hasDkim: boolean;
  hasDmarc: boolean;
  domain: string;
  createdAtUtc: string;
}

export interface RegisterSenderInboxRequest {
  tenantId?: string;
  email: string;
  displayName: string;
  provider: number; // 1 = GoogleWorkspace, 2 = Microsoft365, 3 = CustomSmtp
  dailyQuota: number;
  warmupEnabled: boolean;
  warmupRampDailyIncrement: number;
}

export interface SelectNextSenderResponse {
  selectedSender: MultiInboxAccountDto;
  jitterDelaySeconds: number;
  espMatchStrategy: string;
  calculatedWeight: number;
  scheduledDispatchUtc: string;
  recipientDomain: string;
  detectedRecipientEsp: string;
}

export interface DeliverabilityRadarMetricsDto {
  tenantId: string;
  totalInboxes: number;
  activeInboxes: number;
  warmupInboxes: number;
  quarantinedInboxes: number;
  coolingInboxes: number;
  totalDailyCapacity: number;
  remainingDailyCapacity: number;
  totalSentToday: number;
  averageHealthScore: number;
  averageBounceRate: number;
  averageReplyRate: number;
  averageOpenRate: number;
  totalDispatchedLifetime: number;
  totalBouncedLifetime: number;
  totalRepliedLifetime: number;
  dnsHealth: {
    spfPassRate: number;
    dkimPassRate: number;
    dmarcPassRate: number;
    fullyAlignedCount: number;
  };
  providerDistribution: {
    googleWorkspaceCount: number;
    microsoft365Count: number;
    customSmtpCount: number;
  };
  reputationTier: string;
  radarAxes: Array<{
    axisName: string;
    score: number;
    benchmark: number;
  }>;
}

export interface RotationSimulationItemDto {
  senderAccountId: string;
  email: string;
  provider: MailboxProviderType;
  allocatedDispatches: number;
  allocationPercentage: number;
  preQuotaRemaining: number;
  postQuotaRemaining: number;
  healthScore: number;
}

export interface RotationSimulationResultDto {
  totalRequestedBatchSize: number;
  successfullyAllocated: number;
  unallocatedDueToExhaustion: number;
  averageJitterSeconds: number;
  minJitterSeconds: number;
  maxJitterSeconds: number;
  allocations: RotationSimulationItemDto[];
}

