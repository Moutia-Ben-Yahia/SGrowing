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

// Horizon 16: Autonomous P2P Mailbox Warmup Network & ESP Spam Rescue Daemon
export type WarmupProvider = 'GoogleWorkspace' | 'Microsoft365' | 'GenericSmtp';
export type WarmupStatus = 'WarmingUp' | 'Healthy' | 'SpamAlert' | 'Paused';

export interface EnrolledWarmupMailboxDto {
  id: string;
  tenantId: string;
  email: string;
  domain: string;
  provider: WarmupProvider;
  dailyVolumeCap: number;
  currentRampDay: number;
  recommendedVolumeToday: number;
  sentToday: number;
  receivedToday: number;
  replyCount: number;
  spamRescuesCount: number;
  replyRate: number;
  status: WarmupStatus;
  enrolledAtUtc: string;
  lastActiveAtUtc: string;
}

export interface EnrollMailboxRequest {
  tenantId?: string;
  email: string;
  provider: string;
  dailyVolumeCap?: number;
  startingDay?: number;
}

export interface WarmupDialogueTurn {
  turnIndex: number;
  senderEmail: string;
  recipientEmail: string;
  subject: string;
  body: string;
  signature: string;
  messageId: string;
  inReplyTo?: string;
  dwellDelaySeconds: number;
  requiresReply: boolean;
}

export interface WarmupExchangeResult {
  exchangeId: string;
  subject: string;
  senderEmail: string;
  peerEmail: string;
  warmupToken: string;
  turns: WarmupDialogueTurn[];
  createdAtUtc: string;
}

export interface SpamRescueLogEntry {
  rescueId: string;
  mailboxId: string;
  mailboxEmail: string;
  subject: string;
  warmupToken: string;
  movedToInbox: boolean;
  flaggedNotSpam: boolean;
  markedImportant: boolean;
  starred: boolean;
  humanReadingDelaySeconds: number;
  positiveReplySent: boolean;
  espMlClassifierSignal: string;
  rescuedAtUtc: string;
}

export interface SpamRescueScanResult {
  scannedMailboxesCount: number;
  spamMessagesFound: number;
  rescuedMessagesCount: number;
  markedImportantCount: number;
  starredCount: number;
  repliesDrafted: number;
  achievedReplyRatio: number;
  espClassifierImpact: string;
  rescueLogs: SpamRescueLogEntry[];
}

export interface SpfEvaluationResult {
  hasSpf: boolean;
  rawRecord?: string;
  syntaxValid: boolean;
  dnsLookupCount: number;
  exceedsLookupLimit: boolean;
  qualifier: string;
  status: 'Pass' | 'Warning' | 'Fail';
  details: string;
}

export interface DkimEvaluationResult {
  hasDkim: boolean;
  selector: string;
  rawRecord?: string;
  keyLengthBits: number;
  isRecommendedLength: boolean;
  status: 'Pass' | 'Warning' | 'Fail';
  details: string;
}

export interface DmarcEvaluationResult {
  hasDmarc: boolean;
  rawRecord?: string;
  policy: string;
  percentage: number;
  hasRua: boolean;
  hasRuf: boolean;
  status: 'Pass' | 'Warning' | 'Fail';
  details: string;
}

export interface BimiEvaluationResult {
  hasBimi: boolean;
  rawRecord?: string;
  svgUrl?: string;
  hasValidSvgFormat: boolean;
  hasVmcCertificate: boolean;
  status: 'Pass' | 'Warning' | 'Fail';
  details: string;
}

export interface BlacklistCheckResult {
  name: string;
  host: string;
  isListed: boolean;
  responseTimeMs: number;
  category: string;
  remediationUrl: string;
}

export interface DeliverabilityHealthScore {
  score: number;
  grade: string;
  status: string;
  factorScores: Record<string, number>;
  recommendations: string[];
}

export interface DnsAndBlacklistAuditResult {
  domain: string;
  spf: SpfEvaluationResult;
  dkim: DkimEvaluationResult;
  dmarc: DmarcEvaluationResult;
  bimi: BimiEvaluationResult;
  blacklists: BlacklistCheckResult[];
  totalBlacklistsChecked: number;
  blacklistsCleanCount: number;
  blacklistsListedCount: number;
  overallHealthScore: DeliverabilityHealthScore;
  auditedAtUtc: string;
}

export interface DeliverabilityNetworkMetrics {
  totalEnrolledMailboxes: number;
  activeWarmingMailboxes: number;
  healthyMailboxes: number;
  totalRescuedEmails: number;
  totalSimulatedExchanges: number;
  activeRampTargetVolume: number;
  currentVolumeSentToday: number;
  averageReplyRatioPercent: number;
  averageHealthScore: number;
  blacklistCleanRatePercent: number;
  espClassifierTrainingStatus: string;
}


