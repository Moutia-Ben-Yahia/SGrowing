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
