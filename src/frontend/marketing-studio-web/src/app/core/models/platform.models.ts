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
