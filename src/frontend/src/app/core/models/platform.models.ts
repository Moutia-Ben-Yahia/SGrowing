// Customer Journeys (Phase 3)
export interface CustomerJourneyDto {
  id: string;
  tenantId: string;
  name: string;
  triggerEvent: string;
  status: number; // 1 = Draft, 2 = Active, 3 = Paused
  steps: JourneyStepDto[];
  createdAtUtc: string;
}

export interface JourneyStepDto {
  id: string;
  stepOrder: number;
  stepType: number; // 1 = SendEmail, 2 = SendWhatsApp, 3 = Delay
  subjectTemplate?: string;
  bodyTemplate: string;
  delaySeconds: number;
}

export interface CustomerEventDto {
  id: string;
  contactId: string;
  eventName: string;
  payloadJson: string;
  occurredAtUtc: string;
}

// Feedback & CSAT (Phase 4)
export interface SurveyFormDto {
  id: string;
  tenantId: string;
  title: string;
  type: number; // 1 = Csat, 2 = Nps, 3 = LeadCapture
  questionPrompt: string;
  scaleMax: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface SurveyResponseDto {
  id: string;
  surveyFormId: string;
  contactId: string;
  score: number;
  feedbackText?: string;
  sentiment: number; // 1 = Positive, 2 = Neutral, 3 = Negative
  createdAtUtc: string;
}

// Knowledge Base & RAG (Phase 5)
export interface HelpCategoryDto {
  id: string;
  tenantId: string;
  name: string;
  slug: string;
  orderIndex: number;
  icon: string;
}

export interface HelpArticleDto {
  id: string;
  categoryId: string;
  title: string;
  slug: string;
  contentMarkdown: string;
  isPublished: boolean;
  viewCount: number;
  createdAtUtc: string;
}

export interface RagDeflectionResultDto {
  deflected: boolean;
  answer: string;
  articleTitle?: string;
  articleSlug?: string;
  confidenceScore: number;
}

// WhatsApp Templates & Broadcasts
export interface WhatsAppTemplateDto {
  id: string;
  tenantId: string;
  name: string;
  category: number; // 1 = Marketing, 2 = Utility, 3 = Authentication
  language: string;
  headerText?: string | null;
  bodyText: string;
  footerText?: string | null;
  status: number; // 1 = Draft, 2 = PendingApproval, 3 = Approved, 4 = Rejected
  createdAtUtc: string;
}

export interface BroadcastCampaignDto {
  id: string;
  tenantId: string;
  name: string;
  templateId: string;
  targetTag: string;
  status: number; // 1 = Scheduled, 2 = Processing, 3 = Completed, 4 = Failed
  totalRecipients: number;
  sentCount: number;
  createdAtUtc: string;
}

// In-Chat Booking & Checkout
export interface AppointmentSlotDto {
  id: string;
  tenantId: string;
  title: string;
  durationMinutes: number;
  hostUserId: string;
  isActive: boolean;
}

export interface AppointmentBookingDto {
  id: string;
  slotId: string;
  contactId: string;
  scheduledTimeUtc: string;
  status: number; // 1 = Confirmed, 2 = Cancelled, 3 = Completed
}

export interface CheckoutLinkDto {
  id: string;
  contactId: string;
  conversationId: string;
  title: string;
  amount: number;
  currency: string;
  paymentUrl: string;
  status: number; // 1 = Active, 2 = Paid, 3 = Expired
  createdAtUtc: string;
}

// SLA Policies
export interface SlaPolicyDto {
  id: string;
  tenantId: string;
  name: string;
  priority: number; // 1 = Low, 2 = Normal, 3 = High, 4 = Urgent
  targetFirstResponseMinutes: number;
  targetResolutionMinutes: number;
  isActive: boolean;
}

// Customer 360 Timeline
export interface UnifiedCustomer360Dto {
  contact: any;
  conversations: any[];
  deals: any[];
  events: CustomerEventDto[];
}

// Horizon 15: Event-Driven Customer Journey DAG & Stateful Wait-Until Engine
export enum JourneyNodeType {
  Trigger = 1,
  Delay = 2,
  WaitUntilEvent = 3,
  ConditionBranch = 4,
  ChannelAction = 5,
  Exit = 6
}

export enum JourneyInstanceStatus {
  Active = 1,
  WaitingForEvent = 2,
  Completed = 3,
  Unenrolled = 4
}

export interface JourneyNodeDto {
  id: string;
  name: string;
  type: JourneyNodeType;
  nextNodeId?: string | null;
  successNextNodeId?: string | null;
  timeoutNextNodeId?: string | null;
  trueNextNodeId?: string | null;
  falseNextNodeId?: string | null;
  eventName?: string | null;
  delaySeconds: number;
  timezone?: string | null;
  ttlSeconds: number;
  conditionExpression?: string | null;
  channelType?: string | null;
  actionPayload?: string | null;
  positionX: number;
  positionY: number;
}

export interface JourneyDefinitionDto {
  id: string;
  tenantId: string;
  name: string;
  description: string;
  triggerEvent: string;
  conversionEvents: string[];
  nodes: JourneyNodeDto[];
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface JourneyStepExecutionRecordDto {
  nodeId: string;
  nodeName: string;
  nodeType: JourneyNodeType;
  status: string;
  details: string;
  executedAtUtc: string;
}

export interface JourneyInstanceDto {
  id: string;
  tenantId: string;
  journeyDefinitionId: string;
  contactId: string;
  currentNodeId: string;
  status: JourneyInstanceStatus;
  waitUntilEventName?: string | null;
  waitUntilExpiresAtUtc?: string | null;
  nextExecutionAtUtc?: string | null;
  customerAttributes: Record<string, any>;
  executionHistory: JourneyStepExecutionRecordDto[];
  enrolledAtUtc: string;
  completedAtUtc?: string | null;
}

export interface JourneyNodeMetricDto {
  nodeId: string;
  nodeName: string;
  nodeType: JourneyNodeType;
  executionsCount: number;
  currentContactsCount: number;
  conversionPercent: number;
  dropOffCount: number;
}

export interface JourneyMetricsResponseDto {
  journeyId: string;
  journeyName: string;
  totalEnrolled: number;
  currentlyActive: number;
  currentlyWaiting: number;
  totalCompleted: number;
  totalUnenrolled: number;
  completionRate: number;
  dropOffRate: number;
  nodeMetrics: JourneyNodeMetricDto[];
}

export interface DagValidationResultDto {
  isValid: boolean;
  errorMessage?: string | null;
  cyclePath?: string[] | null;
}

export interface JourneyEventIngestResultDto {
  ingestedEventName: string;
  unenrolledCount: number;
  advancedCount: number;
  enrolledCount: number;
  message: string;
}

