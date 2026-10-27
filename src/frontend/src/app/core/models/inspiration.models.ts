export interface ChatbotFlowNodeDto {
  nodeId: string;
  nodeType: number; // 1: Message, 2: UserInput, 3: DecisionSplitter, 4: Delay, 5: Webhook, 6: Script
  title: string;
  parameters: Record<string, any>;
  nextNodeIds: string[];
}

export interface ChatbotFlowDefinitionDto {
  flowId: string;
  name: string;
  channel: string;
  nodes: ChatbotFlowNodeDto[];
  entryNodeId: string;
}

export interface FlowExecutionResultDto {
  flowId: string;
  currentNodeId: string;
  isCompleted: boolean;
  isWaitingForUserInput: boolean;
  variables: Record<string, any>;
  dispatchedMessages: string[];
  waitingInputPrompt?: string;
}

export interface SocialCommentReactionDto {
  publicReplyComment: string;
  initiatedPrivateDm: boolean;
  privateDmInitialMessage?: string;
  triggeredFlowId?: string;
}

export interface InAppTourStepDto {
  stepOrder: number;
  targetCssSelector: string;
  title: string;
  contentMarkdown: string;
  placement: number; // 1: Top, 2: Bottom, 3: Left, 4: Right
}

export interface InAppTourDto {
  tourId: string;
  name: string;
  targetUrlPathPattern: string;
  steps: InAppTourStepDto[];
  isActive: boolean;
}

export interface InAppBannerDto {
  bannerId: string;
  title: string;
  bodyText: string;
  bannerType: string;
  ctaButtonText: string;
  ctaUrl: string;
  audienceSegment: string;
  isDismissible: boolean;
}

export interface OperatorPresenceDto {
  conversationId: string;
  operatorId: string;
  operatorName: string;
  isTyping: boolean;
  lastSeenUtc: string;
}

export interface SlaBreachAlertDto {
  conversationId: string;
  customerName: string;
  priority: number;
  policyName: string;
  elapsedMinutes: number;
  targetMinutes: number;
  isBreached: boolean;
  minutesRemaining: number;
}

export interface CsatFeedbackDto {
  feedbackId: string;
  conversationId: string;
  ratingScore: number;
  feedbackComment?: string;
  submittedAtUtc: string;
}

export interface WarmupPeerPairDto {
  senderAccountId: string;
  senderEmail: string;
  peerEmail: string;
  dailyWarmupQuota: number;
  currentSentToday: number;
  currentReceivedToday: number;
  isActive: boolean;
}

export interface SpamRescueSummaryDto {
  totalScanned: number;
  rescuedFromSpam: number;
  markedImportant: number;
  movedToInbox: number;
  lastRunUtc: string;
}

export interface DnsDeliverabilityReportDto {
  domain: string;
  hasSpfRecord: boolean;
  spfRecordValue: string;
  hasDkimRecord: boolean;
  dkimKeyLengthBits: number;
  hasDmarcRecord: boolean;
  dmarcPolicy: string;
  hasBimiRecord: boolean;
  hasMxRecord: boolean;
  deliverabilityHealthScore: number;
}

export interface InboxSlotAllocationDto {
  senderAccountId: string;
  senderEmail: string;
  sendingDomain: string;
  dailyCap: number;
  allocatedSlotsRemaining: number;
  pacingSecondsDelay: number;
}

export interface CallSessionInitiationDto {
  callSessionId: string;
  prospectPhone: string;
  agentPhone: string;
  webRtcRoomToken: string;
  status: string;
  startedAtUtc: string;
}

export interface ClassifiedReplyDto {
  inboundSubject: string;
  inboundBody: string;
  category: number; // 1: Interested, 2: MeetingBooked, 3: NotInterested, 4: OutOfOffice, 5: WrongPerson, 6: Unsubscribe
  confidenceScore: number;
  recommendedAction: string;
  autoCalendarLinkInjected: boolean;
}

export interface DurablePublishResultDto {
  postId: string;
  platform: number;
  status: number;
  remotePostId?: string;
  errorMessage?: string;
  completedAtUtc: string;
}

export interface ViralHookSuggestionDto {
  hookText: string;
  angle: string;
  estimatedViralityScore: number;
}

export interface PlatformAdaptedPostDto {
  platform: number;
  formattedContent: string;
  recommendedHashtags: string[];
  characterCount: number;
}

export interface McpToolDefinitionDto {
  name: string;
  description: string;
  inputSchema: Record<string, any>;
}

export interface ContactFactDto {
  factId: string;
  contactId: string;
  factType: string;
  factValue: string;
  confidence: number;
  extractedAtUtc: string;
}

export interface ContactBriefDto {
  contactId: string;
  contactName: string;
  companyName: string;
  keyBulletPoints: string[];
  buyingIntentLevel: string;
  lastUpdatedUtc: string;
}

export interface RottenDealAlertDto {
  dealId: string;
  title: string;
  stage: string;
  value: number;
  daysInCurrentStage: number;
  rottenThresholdDays: number;
  isRotten: boolean;
  recommendedAction: string;
}

export interface TicketCommercialContextDto {
  ticketId: string;
  customerName: string;
  customerEmail: string;
  recentOrders: any[];
  recentInvoices: any[];
  warrantyTier: string;
  hasActiveSubscription: boolean;
}

export interface CpqLineItemDto {
  sku: string;
  description: string;
  quantity: number;
  unitPrice: number;
  taxRatePercentage: number;
  discountPercentage: number;
  totalAmount: number;
}

export interface CpqQuoteDto {
  quoteId: string;
  quoteNumber: string;
  customerName: string;
  customerEmail: string;
  currency: string;
  lineItems: CpqLineItemDto[];
  subtotal: number;
  totalDiscount: number;
  totalTax: number;
  grandTotal: number;
  validUntilUtc: string;
  status: string;
}

export interface EavAttributeDto {
  attributeId: string;
  entityType: string;
  entityId: string;
  attributeKey: string;
  attributeValue: string;
  dataType: string;
}

export interface CryptographicSealResultDto {
  documentId: string;
  sha256Hash: string;
  signatureFormat: string;
  certificateIssuer: string;
  hasRfc3161Timestamp: boolean;
  timestampUtc: string;
  sealedPdfUrl: string;
  tamperProofVerificationToken: string;
}

export interface CompletionCertificateDto {
  documentId: string;
  documentTitle: string;
  signerName: string;
  signerEmail: string;
  signerIpAddress: string;
  userAgent: string;
  signedAtUtc: string;
  documentChecksumSha256: string;
  tsaAuthorityName: string;
  verificationUrl: string;
}
