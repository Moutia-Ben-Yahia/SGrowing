export interface SlaTimerDto {
  slaType: string;
  targetMinutes: number;
  elapsedMinutes: number;
  minutesRemaining: number;
  isBreached: boolean;
  isPaused: boolean;
  pausedMinutes: number;
  pausedAtUtc?: string | null;
  statusBadge: string;
}

export interface CollisionWarningDto {
  hasCollision: boolean;
  warningMessage: string;
  otherAgentNames: string[];
  isAnotherAgentTyping: boolean;
}

export interface CustomerOrderSummaryDto {
  orderNumber: string;
  status: string;
  trackingNumber: string;
  totalAmount: number;
  currency: string;
  courierName: string;
  orderDateUtc: string;
}

export interface Customer360OverviewDto {
  contactId: string;
  primaryName: string;
  primaryEmail: string;
  primaryPhone: string;
  lifecycleStage: string;
  totalOrders: number;
  totalSpent: number;
  deliverySuccessRate: number;
  recentOrders: CustomerOrderSummaryDto[];
  knowledgeBaseTags: string[];
}

export interface UnifiedConversationSummaryDto {
  conversationId: string;
  tenantId: string;
  contactId: string;
  customerName: string;
  customerAvatarUrl?: string | null;
  customerContactInfo?: string | null;
  channel: string; // WhatsApp, Instagram, Messenger, Email, WebChat
  status: number; // 1: Open, 2: Snoozed, 3: Resolved, 4: WaitingOnCustomer
  priority: number; // 1: Low, 2: Normal, 3: High, 4: Urgent
  unreadCount: number;
  snippet: string;
  lastMessageAtUtc: string;
  createdAtUtc: string;
  assigneeId?: string | null;
  assigneeName?: string | null;
  firstResponseSla: SlaTimerDto;
  resolutionSla: SlaTimerDto;
  collisionWarning?: CollisionWarningDto | null;
}

export interface UnifiedDeskMessageDto {
  id: string;
  conversationId: string;
  senderType: number; // 1: Contact, 2: Agent, 3: Bot, 4: System, 5: InternalNote
  senderId?: string | null;
  senderName?: string | null;
  content: string;
  isPrivateNote: boolean;
  deliveryStatus: number;
  createdAtUtc: string;
}

export interface SendUnifiedDeskMessageRequest {
  content: string;
  senderType?: number;
  senderId?: string | null;
  senderName?: string | null;
  isPrivateNote?: boolean;
  bypassSoftLock?: boolean;
}

export interface SendUnifiedDeskMessageResponse {
  success: boolean;
  message?: UnifiedDeskMessageDto | null;
  softLockTriggered: boolean;
  softLockWarning?: string | null;
  secondsSinceLastAgentReply?: number | null;
}

export interface UpdateAgentPresenceRequest {
  agentId: string;
  agentName: string;
  isTyping: boolean;
}

export interface AgentPresenceRecordDto {
  agentId: string;
  agentName: string;
  isTyping: boolean;
  lastSeenUtc: string;
}

export interface AgentPresenceReportDto {
  conversationId: string;
  hasCollision: boolean;
  warningMessage?: string | null;
  activeAgents: AgentPresenceRecordDto[];
}

export interface CopilotSuggestedReplyDto {
  id: string;
  title: string;
  suggestedText: string;
  confidenceScore: number;
  groundingSource: string;
  category: string;
}

export interface AutopilotResolutionDto {
  intent: string;
  confidenceScore: number;
  automatedReply: string;
  actionTaken: string;
  canAutoResolve: boolean;
  suggestedStatus: number;
  dispatchedMessage?: UnifiedDeskMessageDto | null;
}

export interface SlaOverviewReportDto {
  totalActiveConversations: number;
  pausedOnHoldCount: number;
  breachedCount: number;
  atRiskCount: number;
  atRiskOrBreachedConversations: UnifiedConversationSummaryDto[];
}

export interface UnifiedConversationDetailDto {
  summary: UnifiedConversationSummaryDto;
  messages: UnifiedDeskMessageDto[];
  customer360: Customer360OverviewDto;
  presence: AgentPresenceReportDto;
  copilotSuggestions: CopilotSuggestedReplyDto[];
}
