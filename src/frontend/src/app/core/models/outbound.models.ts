export interface SenderAccountDto {
  id: string;
  tenantId: string;
  email: string;
  displayName: string;
  provider: number; // 1 = GoogleWorkspace, 2 = Microsoft365, 3 = CustomSmtp
  dailyQuota: number;
  sentTodayCount: number;
  warmupEnabled: boolean;
  warmupRampDailyIncrement: number;
  healthScore: number;
  isActive: boolean;
}

export interface CadenceStepDto {
  stepNumber: number;
  delayDays: number;
  channel: number; // 1 = Email, 2 = WhatsApp, 3 = LinkedIn
  subjectTemplate: string;
  bodyTemplate: string;
}

export interface CampaignDto {
  id: string;
  tenantId: string;
  name: string;
  status: number; // 1 = Draft, 2 = Active, 3 = Paused, 4 = Completed
  steps: CadenceStepDto[];
  createdAtUtc: string;
}

export interface DealDto {
  id: string;
  tenantId: string;
  contactId: string;
  conversationId?: string;
  title: string;
  amount: number;
  stage: number; // 1 = Lead, 2 = Qualified, 3 = Proposal, 4 = Won, 5 = Lost
  notes?: string;
  createdAtUtc: string;
}

export interface DialerQueueItemDto {
  contactId: string;
  fullName: string;
  companyName: string;
  phoneNumber: string;
  email: string;
  timeZone: string;
  stage: string;
  touchpointStep: number;
  notes?: string;
}

export interface CallOutcomeResultDto {
  activityId: string;
  contactId: string;
  outcome: string;
  durationSeconds: number;
  nextActionDescription: string;
  loggedAtUtc: string;
}
