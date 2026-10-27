// Pillar 1: Cold Outreach & AI SDR Engine
export interface SpintaxPreviewResponse {
  deterministicVariant: string;
  sampleVariants: string[];
  totalPermutations: number;
}

export interface EmailVerificationResult {
  email: string;
  syntaxValid: boolean;
  isDisposable: boolean;
  domain: string;
  hasMxRecords: boolean;
  mxHosts: string[];
  smtpConnectionPassed: boolean;
  isCatchAll: boolean;
  riskScore: number;
  status: 'Deliverable' | 'Risky' | 'Undeliverable' | 'Disposable' | 'CatchAll';
  reason: string;
}

export interface EnrichedContactDto {
  email: string;
  firstName: string;
  lastName: string;
  jobTitle: string;
  seniorityLevel: string;
  companyName: string;
  domain: string;
  industry: string;
  employeeRange: string;
  revenueRange: string;
  techStack: string;
  linkedinUrl: string;
  deliverability: EmailVerificationResult;
  overallDataConfidenceScore: number;
}

// Pillar 2: Deliverability Warmup & Swarm
export interface WarmupTurnMessage {
  turnIndex: number;
  senderEmail: string;
  recipientEmail: string;
  subject: string;
  body: string;
  messageId: string;
  inReplyTo?: string;
  references?: string;
  markAsImportant: boolean;
  starMessage: boolean;
  rescueFromSpam: boolean;
  emulatedReadingDelaySeconds: number;
}

export interface WarmupDialogueThread {
  threadId: string;
  topic: string;
  threadSubject: string;
  turns: WarmupTurnMessage[];
}

export interface EspPlacementSummary {
  esp: string;
  totalSeeds: number;
  inboxCount: number;
  spamCount: number;
  promotionsCount: number;
  inboxPlacementPercentage: number;
}

export interface SeedPlacementReportDto {
  testId: string;
  senderEmail: string;
  subject: string;
  testedAtUtc: string;
  totalSeedsTested: number;
  overallInboxPlacementPercentage: number;
  overallSpamPercentage: number;
  espBreakdowns: EspPlacementSummary[];
  inboxingRecommendations: string[];
}

// Pillar 3: Omnichannel Inbox & Support
export interface ParsedMimeEmail {
  subject: string;
  from: string;
  to: string;
  cc?: string;
  messageId?: string;
  inReplyTo?: string;
  references?: string;
  dateUtc?: string;
  contentType: string;
  textBody: string;
  cleanTextBody: string;
  htmlBody: string;
  attachments: { fileName: string; contentType: string; contentId: string; isInline: boolean; sizeBytes: number }[];
}

export interface CannedMacroDto {
  id: string;
  tenantId: string;
  triggerKey: string;
  title: string;
  category: string;
  contentTemplate: string;
  requiredVariables: string[];
}

export interface CannedMacroResolutionDto {
  triggerKey: string;
  title: string;
  resolvedContent: string;
  missingVariables: string[];
}

export interface CsatSurveyDispatchResult {
  surveyId: string;
  conversationId: string;
  channel: string;
  surveyUrl: string;
  formattedMessage: string;
  dispatchedAtUtc: string;
}

export interface SlaEscalationItemDto {
  conversationId: string;
  priority: number;
  policyName: string;
  elapsedMinutes: number;
  targetMinutes: number;
  escalationTier: string;
  priorityElevated: boolean;
  recommendedAction: string;
  detectedAtUtc: string;
}

export interface SlaEscalationEvaluationResult {
  tenantId: string;
  evaluatedAtUtc: string;
  totalActiveEvaluated: number;
  totalWarning: number;
  totalBreached: number;
  totalCritical: number;
  escalations: SlaEscalationItemDto[];
}

// Pillar 4: B2B CRM & Data Operations
export interface CsvImportPreviewResult {
  totalRows: number;
  detectedHeaders: string[];
  sampleRows: Record<string, string>[];
  previewErrors: { rowNumber: number; email?: string; fieldName: string; errorMessage: string }[];
}

export interface CsvImportExecutionResult {
  totalProcessed: number;
  insertedCount: number;
  updatedCount: number;
  skippedDuplicatesCount: number;
  errorCount: number;
  wasRolledBack: boolean;
  statusSummary: string;
  errors: { rowNumber: number; email?: string; fieldName: string; errorMessage: string }[];
}

// Pillar 5: Conversational Bot Canvas
export interface BotCardButton {
  title: string;
  actionType: string;
  value: string;
}

export interface BotCarouselCard {
  title: string;
  subtitle?: string;
  imageUrl?: string;
  buttons: BotCardButton[];
}

export interface BotTranscriptMessage {
  sender: 'Bot' | 'User';
  content: string;
  cards?: BotCarouselCard[];
  isTypingIndicator: boolean;
  delayMilliseconds?: number;
  timestampUtc: string;
}

export interface BotSimulationSession {
  sessionId: string;
  workflowId: string;
  currentNodeId?: string;
  variables: Record<string, any>;
  transcript: BotTranscriptMessage[];
  isWaitingForUserInput: boolean;
  isHandedOverToHuman: boolean;
  isCompleted: boolean;
  totalStepsExecuted: number;
}

export interface BotSimulationStepResult {
  sessionId: string;
  currentNodeId?: string;
  newMessages: BotTranscriptMessage[];
  updatedVariables: Record<string, any>;
  isWaitingForUserInput: boolean;
  isHandedOverToHuman: boolean;
  isCompleted: boolean;
  executionTraceLogs: string[];
}

// Pillar 6: CPQ & Cryptographic E-Signatures
export interface VisualPdfFieldDto {
  fieldId: string;
  pageNumber: number;
  xCoordinate: number;
  yCoordinate: number;
  width: number;
  height: number;
  fieldType: number; // 0: Signature, 1: Initials, 2: DateSigned, 3: TextBox, 4: Checkbox
  signerEmail: string;
  isRequired: boolean;
  placeholder?: string;
  value?: string;
}

export interface ContractSignerDto {
  signerId: string;
  orderIndex: number;
  fullName: string;
  email: string;
  phoneNumber?: string;
  role: number;
  status: number; // 0: Pending, 1: Sent, 2: Viewed, 3: OtpVerified, 4: Signed, 5: Declined
  signingToken: string;
  requiresOtp: boolean;
  isOtpVerified: boolean;
  sentAtUtc?: string;
  signedAtUtc?: string;
}

export interface MultiPartyContractDto {
  contractId: string;
  tenantId: string;
  title: string;
  documentUrl: string;
  workflowType: number; // 0: Sequential, 1: Parallel
  overallStatus: string;
  signers: ContractSignerDto[];
  visualFields: VisualPdfFieldDto[];
  currentSequentialOrderIndex: number;
  sealedPdfUrl?: string;
  checksumSha256?: string;
}

export interface OtpVerificationResult {
  success: boolean;
  message: string;
  remainingAttempts: number;
  isVerified: boolean;
}

// Pillar 7: COD Logistics & Anti-Fraud
export interface WaybillGenerationResult {
  courierCode: string;
  courierName: string;
  trackingNumber: string;
  awbBarcode: string;
  labelUrl: string;
  manifestId: string;
  declaredCashToCollect: number;
  status: number;
  dispatchedAtUtc: string;
}

export interface TrackingCheckpointDto {
  timestampUtc: string;
  location: string;
  statusDescription: string;
  standardizedStatus: number;
}

export interface CarrierTrackingResult {
  courierCode: string;
  trackingNumber: string;
  currentStatus: number;
  statusDescription: string;
  cashCollected: number;
  checkpoints: TrackingCheckpointDto[];
}

export interface ShippingLabelResult {
  trackingNumber: string;
  format: number; // 0: Pdf, 1: Zpl, 2: Png
  labelContentBase64OrUrl: string;
  pageCount: number;
}

export interface CodRiskFactor {
  ruleId: string;
  description: string;
  pointsAdded: number;
  severity: string;
}

export interface CodFraudEvaluationResult {
  riskScore: number;
  tier: number; // 0: Safe, 1: Moderate, 2: HighRisk, 3: Fraudulent
  recommendedAction: number; // 0: AutoApprove, 1: RequireSmsOtpConfirmation, 2: RequirePrepaymentDeposit, 3: AutoReject
  triggeredRules: CodRiskFactor[];
  assessmentSummary: string;
  evaluatedAtUtc: string;
}
