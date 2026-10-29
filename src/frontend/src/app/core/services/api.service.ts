import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ContactDto, ConversationDto, MessageDto } from '../models/chat.models';
import { SenderAccountDto, CampaignDto, DealDto, DialerQueueItemDto, CallOutcomeResultDto } from '../models/outbound.models';
import { 
  CustomerJourneyDto, 
  SurveyFormDto, 
  SurveyResponseDto, 
  HelpArticleDto, 
  RagDeflectionResultDto,
  WhatsAppTemplateDto,
  BroadcastCampaignDto,
  AppointmentSlotDto,
  AppointmentBookingDto,
  CheckoutLinkDto,
  SlaPolicyDto,
  UnifiedCustomer360Dto
} from '../models/platform.models';
import { SeoAuditReportDto, PageAuditResultDto, SerpKeywordTrackingDto, AeoBrandMentionDto, GeoScoreReportDto } from '../models/seo.models';
import { SocialChannelMetadataDto, SocialAccountDto, SocialPostDto, DeflectCommentResultDto, TokenHealthAuditReportDto } from '../models/social.models';
import { SalesFunnelDto, FunnelStepDto, OrderBumpOfferDto, OneClickUpsellOfferDto, FunnelPerformanceSummaryDto } from '../models/funnel.models';
import { BookingEventTypeDto, CalendarBookingRecordDto, TimeSlotDto, HostAvailabilityScheduleDto, HostWeightDto, VideoMeetingSessionDto, WebRtcAccessTokenResultDto, ParticipantMediaStateDto, ParticipantStateDto, SalesBattleCardDto, ObjectionEvaluationResultDto, MeetingSessionSummaryDto } from '../models/calendar.models';
import { 
  DomainDnsHealthRecordDto, 
  EmailSpamAnalysisReportDto, 
  MailboxWarmupCadenceDto, 
  WarmupExchangeLogDto,
  MultiInboxAccountDto,
  RegisterSenderInboxRequest,
  SelectNextSenderResponse,
  DeliverabilityRadarMetricsDto,
  RotationSimulationResultDto
} from '../models/deliverability.models';
import { LiveTelemetrySummaryDto, TelemetryEventDto } from '../models/telemetry.models';
import { SubscriptionDetailsDto, BillingInvoiceRecordDto } from '../models/billing.models';
import { DigitalAssetDto, DigitalDownloadTokenDto, ProductLicenseKeyDto, PimVariantMatrixDto } from '../models/digital-goods.models';
import { DynamicFormDefinitionDto, DynamicFormSubmissionDto, CryptographicPdfRecordDto } from '../models/dynamic-forms.models';
import { TenantApiKeyDto, WebhookSubscriptionRecordDto, WebhookDeliveryAttemptLogDto } from '../models/developer-webhooks.models';
import {
  DriverMobileSessionDto,
  DoorstepPodDto,
  DriverCashSummaryDto,
  WilayaZoneDto,
  CodCheckoutSessionDto,
  UgcVideoScriptDto,
  CompetitorAdRecordDto,
  CompetitorPriceTrackerDto,
  CompetitorRadarSummaryDto,
  B2BCreditAccountDto,
  FactoringReceivableRecordDto
} from '../models/horizons-advanced.models';
import {
  ChatbotFlowNodeDto,
  ChatbotFlowDefinitionDto,
  FlowExecutionResultDto,
  SocialCommentReactionDto,
  InAppTourDto,
  InAppBannerDto,
  OperatorPresenceDto,
  SlaBreachAlertDto,
  CsatFeedbackDto,
  WarmupPeerPairDto,
  SpamRescueSummaryDto,
  DnsDeliverabilityReportDto,
  InboxSlotAllocationDto,
  CallSessionInitiationDto,
  ClassifiedReplyDto,
  DurablePublishResultDto,
  ViralHookSuggestionDto,
  PlatformAdaptedPostDto,
  McpToolDefinitionDto,
  ContactFactDto,
  ContactBriefDto,
  RottenDealAlertDto,
  TicketCommercialContextDto,
  CpqQuoteDto,
  CpqLineItemDto,
  EavAttributeDto,
  CryptographicSealResultDto,
  CompletionCertificateDto,
  DocumentTelemetrySummaryDto,
  SealVerificationResultDto
} from '../models/inspiration.models';
import {
  SpintaxPreviewResponse,
  EmailVerificationResult,
  EnrichedContactDto,
  WarmupDialogueThread,
  SeedPlacementReportDto,
  ParsedMimeEmail,
  CannedMacroDto,
  CannedMacroResolutionDto,
  CsatSurveyDispatchResult,
  SlaEscalationEvaluationResult,
  CsvImportPreviewResult,
  CsvImportExecutionResult,
  BotSimulationSession,
  BotSimulationStepResult,
  MultiPartyContractDto,
  OtpVerificationResult,
  WaybillGenerationResult,
  CarrierTrackingResult,
  ShippingLabelResult,
  CodFraudEvaluationResult
} from '../models/pillars.models';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  private baseUrl = 'http://localhost:5000/api';

  public getContacts(tenantId: string, query = ''): Observable<ContactDto[]> {
    return this.http.get<ContactDto[]>(`${this.baseUrl}/crm/contacts?tenantId=${tenantId}&query=${query}`);
  }

  public getMessages(conversationId: string, tenantId: string): Observable<MessageDto[]> {
    return this.http.get<MessageDto[]>(`${this.baseUrl}/conversations/${conversationId}/messages?tenantId=${tenantId}`);
  }

  public sendMessage(conversationId: string, payload: any): Observable<MessageDto> {
    return this.http.post<MessageDto>(`${this.baseUrl}/conversations/${conversationId}/messages`, payload);
  }

  public changeStatus(conversationId: string, tenantId: string, status: number): Observable<ConversationDto> {
    return this.http.post<ConversationDto>(`${this.baseUrl}/conversations/${conversationId}/status`, {
      tenantId,
      conversationId,
      status
    });
  }

  public approveAiDraft(draftId: string, tenantId: string): Observable<MessageDto> {
    return this.http.post<MessageDto>(`${this.baseUrl}/ai/drafts/${draftId}/approve`, {
      tenantId,
      draftId
    });
  }

  // Outbound AI SDR & Mailboxes
  public getMailboxes(tenantId: string): Observable<SenderAccountDto[]> {
    return this.http.get<SenderAccountDto[]>(`${this.baseUrl}/outbound/mailboxes?tenantId=${tenantId}`);
  }

  public getCampaigns(tenantId: string): Observable<CampaignDto[]> {
    return this.http.get<CampaignDto[]>(`${this.baseUrl}/outbound/campaigns?tenantId=${tenantId}`);
  }

  public createCampaign(payload: any): Observable<CampaignDto> {
    return this.http.post<CampaignDto>(`${this.baseUrl}/outbound/campaigns`, payload);
  }

  public enrollProspects(campaignId: string, tenantId: string, contactIds: string[]): Observable<{ enrolledCount: number }> {
    return this.http.post<{ enrolledCount: number }>(`${this.baseUrl}/outbound/campaigns/${campaignId}/enroll?tenantId=${tenantId}`, contactIds);
  }

  public triggerDispatch(tenantId: string): Observable<{ processed: number }> {
    return this.http.post<{ processed: number }>(`${this.baseUrl}/outbound/dispatch?tenantId=${tenantId}`, {});
  }

  public getDeals(tenantId: string): Observable<DealDto[]> {
    return this.http.get<DealDto[]>(`${this.baseUrl}/crm/deals?tenantId=${tenantId}`);
  }

  // Customer Journeys (Phase 3)
  public getJourneys(tenantId: string): Observable<CustomerJourneyDto[]> {
    return this.http.get<CustomerJourneyDto[]>(`${this.baseUrl}/journeys?tenantId=${tenantId}`);
  }

  public triggerEvent(tenantId: string, contactId: string, eventName: string, payloadJson = '{}'): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/events?tenantId=${tenantId}`, { contactId, eventName, payloadJson });
  }

  public dispatchJourneys(tenantId: string): Observable<{ processed: number }> {
    return this.http.post<{ processed: number }>(`${this.baseUrl}/journeys/dispatch?tenantId=${tenantId}`, {});
  }

  // Feedback & CSAT (Phase 4)
  public getSurveys(tenantId: string): Observable<SurveyFormDto[]> {
    return this.http.get<SurveyFormDto[]>(`${this.baseUrl}/feedback/surveys?tenantId=${tenantId}`);
  }

  public submitFeedback(tenantId: string, surveyFormId: string, contactId: string, score: number, feedbackText?: string): Observable<SurveyResponseDto> {
    return this.http.post<SurveyResponseDto>(`${this.baseUrl}/feedback/submit?tenantId=${tenantId}`, {
      surveyFormId,
      contactId,
      score,
      feedbackText
    });
  }

  public getFeedbackResponses(tenantId: string): Observable<SurveyResponseDto[]> {
    return this.http.get<SurveyResponseDto[]>(`${this.baseUrl}/feedback/responses?tenantId=${tenantId}`);
  }

  // Knowledge Base & RAG (Phase 5)
  public getArticles(tenantId: string): Observable<HelpArticleDto[]> {
    return this.http.get<HelpArticleDto[]>(`${this.baseUrl}/kb/articles?tenantId=${tenantId}`);
  }

  public queryHelpCenter(tenantId: string, question: string): Observable<RagDeflectionResultDto> {
    return this.http.post<RagDeflectionResultDto>(`${this.baseUrl}/kb/query?tenantId=${tenantId}`, { question });
  }

  // WhatsApp Templates & Broadcasts
  public getWhatsAppTemplates(tenantId: string): Observable<WhatsAppTemplateDto[]> {
    return this.http.get<WhatsAppTemplateDto[]>(`${this.baseUrl}/channels/whatsapp/templates?tenantId=${tenantId}`);
  }

  public createWhatsAppTemplate(template: any): Observable<WhatsAppTemplateDto> {
    return this.http.post<WhatsAppTemplateDto>(`${this.baseUrl}/channels/whatsapp/templates`, template);
  }

  public getBroadcasts(tenantId: string): Observable<BroadcastCampaignDto[]> {
    return this.http.get<BroadcastCampaignDto[]>(`${this.baseUrl}/channels/broadcasts?tenantId=${tenantId}`);
  }

  public createBroadcast(campaign: any): Observable<BroadcastCampaignDto> {
    return this.http.post<BroadcastCampaignDto>(`${this.baseUrl}/channels/broadcasts`, campaign);
  }

  public dispatchBroadcast(campaignId: string, tenantId: string): Observable<{ recipientsMatched: number; sentCount: number }> {
    return this.http.post<{ recipientsMatched: number; sentCount: number }>(`${this.baseUrl}/channels/broadcasts/${campaignId}/dispatch?tenantId=${tenantId}`, {});
  }

  // Booking & Assisted Checkout
  public getAppointmentSlots(tenantId: string): Observable<AppointmentSlotDto[]> {
    return this.http.get<AppointmentSlotDto[]>(`${this.baseUrl}/sales/booking/slots?tenantId=${tenantId}`);
  }

  public bookAppointment(tenantId: string, slotId: string, contactId: string, scheduledTimeUtc: string): Observable<AppointmentBookingDto> {
    return this.http.post<AppointmentBookingDto>(`${this.baseUrl}/sales/booking/book?tenantId=${tenantId}`, {
      slotId,
      contactId,
      scheduledTimeUtc
    });
  }

  public createCheckoutLink(tenantId: string, contactId: string, conversationId: string, title: string, amount: number, currency = 'USD'): Observable<CheckoutLinkDto> {
    return this.http.post<CheckoutLinkDto>(`${this.baseUrl}/sales/checkout/create?tenantId=${tenantId}`, {
      contactId,
      conversationId,
      title,
      amount,
      currency
    });
  }

  public payCheckoutLink(linkId: string, tenantId: string): Observable<CheckoutLinkDto> {
    return this.http.post<CheckoutLinkDto>(`${this.baseUrl}/sales/checkout/${linkId}/pay?tenantId=${tenantId}`, {});
  }

  // SLA Management
  public getSlaPolicies(tenantId: string): Observable<SlaPolicyDto[]> {
    return this.http.get<SlaPolicyDto[]>(`${this.baseUrl}/sla/policies?tenantId=${tenantId}`);
  }

  public createSlaPolicy(policy: any): Observable<SlaPolicyDto> {
    return this.http.post<SlaPolicyDto>(`${this.baseUrl}/sla/policies`, policy);
  }

  public evaluateSla(tenantId: string): Observable<{ breachedCount: number }> {
    return this.http.post<{ breachedCount: number }>(`${this.baseUrl}/sla/evaluate?tenantId=${tenantId}`, {});
  }

  // Customer 360 Timeline
  public getCustomerTimeline(contactId: string, tenantId: string): Observable<UnifiedCustomer360Dto> {
    return this.http.get<UnifiedCustomer360Dto>(`${this.baseUrl}/crm/contacts/${contactId}/timeline?tenantId=${tenantId}`);
  }

  // Autonomous Fulfillment & Operations Expansion
  public queueVoiceDialer(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/voice/dialer/queue`, payload);
  }

  public processVoiceTurn(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/voice/dialer/process-turn`, payload);
  }

  // Frontier 1: Real-Time Voice AI & Telephony Stream
  public streamVoiceTurn(payload: any, tenantId?: string): Observable<any> {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.http.post<any>(`${this.baseUrl}/voice/amd/stream-turn${query}`, payload);
  }

  public analyzeVoiceSentiment(speechText: string): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/voice/amd/sentiment-analyze`, { speechText });
  }

  public sendVoiceTranscriptionChunk(payload: any, tenantId?: string): Observable<any> {
    const query = tenantId ? `?tenantId=${tenantId}` : '';
    return this.http.post<any>(`${this.baseUrl}/voice/amd/transcription-chunk${query}`, payload);
  }

  public getVoiceSessionTelemetry(sessionId: string): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/voice/amd/session/${sessionId}/telemetry`);
  }

  public createPickSlip(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/fulfillment/pickslips`, payload);
  }

  public scanPickItem(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/fulfillment/pickslips/scan`, payload);
  }

  public completePack(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/fulfillment/pickslips/complete-pack`, payload);
  }

  public scanManifestParcel(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/fulfillment/manifests/scan`, payload);
  }

  public closeManifest(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/fulfillment/manifests/close-sign`, payload);
  }

  public inspectRma(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/fulfillment/rma/inspect`, payload);
  }

  public syncMetaCatalog(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/channels/whatsapp/catalog/sync`, payload);
  }

  public registerDomain(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/storefronts/domains/register`, payload);
  }

  public verifyDomain(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/storefronts/domains/verify-dns`, payload);
  }

  public upsertBrandTheme(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/storefronts/branding/tokens`, payload);
  }

  public evaluateReputation(tenantId: string, phone: string, email?: string): Observable<any> {
    const q = email ? `&email=${encodeURIComponent(email)}` : '';
    return this.http.get<any>(`${this.baseUrl}/commerce/reputation/evaluate?tenantId=${tenantId}&phone=${encodeURIComponent(phone)}${q}`);
  }

  public generateDepositLink(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/reputation/deposit-link`, payload);
  }

  public settleDeposit(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/reputation/settle-deposit`, payload);
  }

  // Smart Courier Routing
  public getSmartRoutingRanking(tenantId: string, governorate: string, weight = 1.0, codAmount = 100): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/commerce/routing/rank?tenantId=${tenantId}&governorate=${encodeURIComponent(governorate)}&weight=${weight}&codAmount=${codAmount}`);
  }

  public autoAssignCarrier(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/routing/auto-assign`, payload);
  }

  public slaBreachFallback(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/routing/sla-breach-fallback`, payload);
  }

  // Post-Purchase Retention & Replenishment
  public scheduleRetention(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/marketing/retention/schedule`, payload);
  }

  public submitSurvey(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/marketing/retention/survey-response`, payload);
  }

  public processReplenishments(payload: any): Observable<any[]> {
    return this.http.post<any[]>(`${this.baseUrl}/marketing/retention/process-replenishments`, payload);
  }

  public getWinBackCampaign(tenantId: string, inactiveDays = 90): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/marketing/retention/win-back?tenantId=${tenantId}&inactiveDays=${inactiveDays}`);
  }

  // Multi-Warehouse Hubs & Transfers
  public allocateProximityWarehouse(tenantId: string, governorate: string, sku: string, quantity = 1): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/warehouses/proximity-allocate?tenantId=${tenantId}&governorate=${encodeURIComponent(governorate)}&sku=${encodeURIComponent(sku)}&quantity=${quantity}`);
  }

  public createTransfer(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/warehouses/transfers`, payload);
  }

  public dispatchTransfer(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/warehouses/transfers/dispatch`, payload);
  }

  public receiveTransfer(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/warehouses/transfers/receive`, payload);
  }

  public evaluateRop(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/warehouses/evaluate-rop`, payload);
  }

  // Live Social Commerce
  public processLiveComment(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/channels/live/process-comment`, payload);
  }

  public getLiveOverlay(tenantId: string, sessionId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/channels/live/${sessionId}/overlay?tenantId=${tenantId}`);
  }

  // Treasury Float & Cash Flow
  public getFloatValuation(tenantId: string): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/finance/treasury/float?tenantId=${tenantId}`);
  }

  public get14DayCashForecast(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/finance/treasury/forecast-14day?tenantId=${tenantId}`);
  }

  // Creator & Affiliate Escrow
  public getCreators(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/marketing/creators?tenantId=${tenantId}`);
  }

  public registerCreator(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/marketing/creators`, payload);
  }

  public getCreatorSummary(tenantId: string, creatorId: string): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/marketing/creators/${creatorId}/summary?tenantId=${tenantId}`);
  }

  public generatePayoutBatch(payload: any): Observable<any[]> {
    return this.http.post<any[]>(`${this.baseUrl}/marketing/creators/payouts/batch`, payload);
  }

  // Reverse Logistics & Drop-and-Swap
  public getExchangeRequests(tenantId: string, status?: number): Observable<any[]> {
    const url = status !== undefined
      ? `${this.baseUrl}/commerce/exchanges?tenantId=${tenantId}&status=${status}`
      : `${this.baseUrl}/commerce/exchanges?tenantId=${tenantId}`;
    return this.http.get<any[]>(url);
  }

  public requestExchange(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/exchanges/request`, payload);
  }

  public dispatchSwapManifest(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/exchanges/manifest`, payload);
  }

  public confirmDoorstepSwap(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/exchanges/doorstep-confirm`, payload);
  }

  public inspectReturnIntake(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/exchanges/intake-inspect`, payload);
  }

  // Cross-Border Trade & Customs
  public getTradeCountries(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/commerce/cross-border/countries?tenantId=${tenantId}`);
  }

  public getCommercialInvoices(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/commerce/cross-border/invoices?tenantId=${tenantId}`);
  }

  // Demand Forecasting & Restock Matrix
  public getStockoutAlerts(tenantId: string, minPriority?: number): Observable<any[]> {
    const url = minPriority !== undefined
      ? `${this.baseUrl}/commerce/forecasting/alerts?tenantId=${tenantId}&minPriority=${minPriority}`
      : `${this.baseUrl}/commerce/forecasting/alerts?tenantId=${tenantId}`;
    return this.http.get<any[]>(url);
  }

  public getSuppliers(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/commerce/forecasting/suppliers?tenantId=${tenantId}`);
  }

  public getRestockOrders(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/commerce/forecasting/restock-orders?tenantId=${tenantId}`);
  }

  public generateRestockOrder(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/forecasting/restock-orders`, payload);
  }

  // RBAC & Security Audit Logs
  public getSecurityAuditLogs(tenantId: string, resource?: string): Observable<any[]> {
    const url = resource
      ? `${this.baseUrl}/security/audit-logs?tenantId=${tenantId}&resource=${encodeURIComponent(resource)}`
      : `${this.baseUrl}/security/audit-logs?tenantId=${tenantId}`;
    return this.http.get<any[]>(url);
  }

  public getMaskedOrderView(tenantId: string, orderId: string, userRole: number): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/security/orders/${orderId}/masked?tenantId=${tenantId}&userRole=${userRole}`);
  }

  // Developer & Demo Sandbox
  public seedDemoData(tenantId?: string): Observable<any> {
    const url = tenantId ? `${this.baseUrl}/dev/seed-demo?tenantId=${tenantId}` : `${this.baseUrl}/dev/seed-demo`;
    return this.http.post<any>(url, {});
  }

  // Technical SEO & SERP Rank Tracking Hub
  public runSeoAudit(payload: any): Observable<{ report: SeoAuditReportDto; pages: PageAuditResultDto[] }> {
    return this.http.post<{ report: SeoAuditReportDto; pages: PageAuditResultDto[] }>(`${this.baseUrl}/seo/audits/run`, payload);
  }

  public getLatestSeoAudit(tenantId: string): Observable<{ report: SeoAuditReportDto; pages: PageAuditResultDto[] }> {
    return this.http.get<{ report: SeoAuditReportDto; pages: PageAuditResultDto[] }>(`${this.baseUrl}/seo/audits/latest?tenantId=${tenantId}`);
  }

  public trackSeoKeyword(payload: any): Observable<SerpKeywordTrackingDto> {
    return this.http.post<SerpKeywordTrackingDto>(`${this.baseUrl}/seo/keywords`, payload);
  }

  public updateKeywordRank(id: string, payload: any): Observable<SerpKeywordTrackingDto> {
    return this.http.post<SerpKeywordTrackingDto>(`${this.baseUrl}/seo/keywords/${id}/rank`, payload);
  }

  public getTrackedKeywords(tenantId: string): Observable<SerpKeywordTrackingDto[]> {
    return this.http.get<SerpKeywordTrackingDto[]>(`${this.baseUrl}/seo/keywords?tenantId=${tenantId}`);
  }

  public recordAeoMention(payload: any): Observable<AeoBrandMentionDto> {
    return this.http.post<AeoBrandMentionDto>(`${this.baseUrl}/seo/aeo/check`, payload);
  }

  public getAeoMentions(tenantId: string): Observable<AeoBrandMentionDto[]> {
    return this.http.get<AeoBrandMentionDto[]>(`${this.baseUrl}/seo/aeo/mentions?tenantId=${tenantId}`);
  }

  // AI Sales Funnel Builder & 1-Click Upsells
  public getFunnels(tenantId: string): Observable<SalesFunnelDto[]> {
    return this.http.get<SalesFunnelDto[]>(`${this.baseUrl}/funnels?tenantId=${tenantId}`);
  }

  public getFunnel(id: string, tenantId: string): Observable<SalesFunnelDto> {
    return this.http.get<SalesFunnelDto>(`${this.baseUrl}/funnels/${id}?tenantId=${tenantId}`);
  }

  public createFunnel(payload: any): Observable<SalesFunnelDto> {
    return this.http.post<SalesFunnelDto>(`${this.baseUrl}/funnels`, payload);
  }

  public getFunnelSteps(funnelId: string, tenantId: string): Observable<FunnelStepDto[]> {
    return this.http.get<FunnelStepDto[]>(`${this.baseUrl}/funnels/${funnelId}/steps?tenantId=${tenantId}`);
  }

  public addFunnelStep(funnelId: string, payload: any): Observable<FunnelStepDto> {
    return this.http.post<FunnelStepDto>(`${this.baseUrl}/funnels/${funnelId}/steps`, payload);
  }

  public getOrderBumps(funnelId: string, tenantId: string): Observable<OrderBumpOfferDto[]> {
    return this.http.get<OrderBumpOfferDto[]>(`${this.baseUrl}/funnels/${funnelId}/order-bumps?tenantId=${tenantId}`);
  }

  public createOrderBump(funnelId: string, payload: any): Observable<OrderBumpOfferDto> {
    return this.http.post<OrderBumpOfferDto>(`${this.baseUrl}/funnels/${funnelId}/order-bumps`, payload);
  }

  public getOneClickUpsells(funnelId: string, tenantId: string): Observable<OneClickUpsellOfferDto[]> {
    return this.http.get<OneClickUpsellOfferDto[]>(`${this.baseUrl}/funnels/${funnelId}/upsells?tenantId=${tenantId}`);
  }

  public createOneClickUpsell(funnelId: string, payload: any): Observable<OneClickUpsellOfferDto> {
    return this.http.post<OneClickUpsellOfferDto>(`${this.baseUrl}/funnels/${funnelId}/upsells`, payload);
  }

  public getFunnelPerformance(funnelId: string, tenantId: string): Observable<FunnelPerformanceSummaryDto> {
    return this.http.get<FunnelPerformanceSummaryDto>(`${this.baseUrl}/funnels/${funnelId}/performance?tenantId=${tenantId}`);
  }

  public recordFunnelEvent(funnelId: string, payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/funnels/${funnelId}/events`, payload);
  }

  // Next-Gen Strategic Frontiers:
  // 1. Closed-Loop Delivered ROAS & Ad Optimizer
  public getAdOptimizerMetrics(tenantId: string, platform?: number): Observable<any[]> {
    const url = platform !== undefined
      ? `${this.baseUrl}/marketing/ad-optimizer/metrics?tenantId=${tenantId}&platform=${platform}`
      : `${this.baseUrl}/marketing/ad-optimizer/metrics?tenantId=${tenantId}`;
    return this.http.get<any[]>(url);
  }

  public calculateDeliveredRoas(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/marketing/ad-optimizer/calculate-roas`, payload);
  }

  public evaluateAdRules(metricId: string, tenantId: string, dailyBudget: number): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/marketing/ad-optimizer/evaluate/${metricId}?tenantId=${tenantId}&dailyBudget=${dailyBudget}`, {});
  }

  public getAdExecutionLogs(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/marketing/ad-optimizer/logs?tenantId=${tenantId}`);
  }

  // 2. In-House Fleet & Driver Run-Sheets
  public getDrivers(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/commerce/fleet/drivers?tenantId=${tenantId}`);
  }

  public registerDriver(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/fleet/drivers`, payload);
  }

  public generateRunSheet(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/fleet/run-sheets`, payload);
  }

  public deliverRunSheetStop(stopId: string, payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/fleet/stops/${stopId}/deliver`, payload);
  }

  public reconcileDriverCash(runSheetId: string, payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/fleet/run-sheets/${runSheetId}/reconcile`, payload);
  }

  // 3. Visual WhatsApp Flow Engine
  public getFlows(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/channels/flows?tenantId=${tenantId}`);
  }

  public createFlow(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/channels/flows`, payload);
  }

  public executeFlowTurn(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/channels/flows/execute-turn`, payload);
  }

  // 4. Aging Inventory Liquidation & Markdown Matrix
  public getStockAging(tenantId: string, tier?: number): Observable<any[]> {
    const url = tier !== undefined
      ? `${this.baseUrl}/commerce/liquidation/aging?tenantId=${tenantId}&tier=${tier}`
      : `${this.baseUrl}/commerce/liquidation/aging?tenantId=${tenantId}`;
    return this.http.get<any[]>(url);
  }

  public recordStockAging(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/liquidation/intake-aging`, payload);
  }

  public generateLiquidationBundle(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/commerce/liquidation/bundles`, payload);
  }

  public getLiquidationBundles(tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/commerce/liquidation/bundles?tenantId=${tenantId}`);
  }

  // Appointment Scheduling & Calendar Engine (Category 16)
  public getEventTypes(tenantId: string): Observable<BookingEventTypeDto[]> {
    return this.http.get<BookingEventTypeDto[]>(`${this.baseUrl}/calendar/event-types?tenantId=${tenantId}`);
  }

  public createEventType(payload: any): Observable<BookingEventTypeDto> {
    return this.http.post<BookingEventTypeDto>(`${this.baseUrl}/calendar/event-types`, payload);
  }

  public getAvailableSlots(tenantId: string, eventTypeId: string, date: string): Observable<TimeSlotDto[]> {
    return this.http.get<TimeSlotDto[]>(`${this.baseUrl}/calendar/availability/slots?tenantId=${tenantId}&eventTypeId=${eventTypeId}&date=${date}`);
  }

  public createCalendarBooking(payload: any): Observable<CalendarBookingRecordDto> {
    return this.http.post<CalendarBookingRecordDto>(`${this.baseUrl}/calendar/bookings`, payload);
  }

  public getBookings(tenantId: string, from?: string, to?: string): Observable<CalendarBookingRecordDto[]> {
    let url = `${this.baseUrl}/calendar/bookings?tenantId=${tenantId}`;
    if (from) url += `&from=${from}`;
    if (to) url += `&to=${to}`;
    return this.http.get<CalendarBookingRecordDto[]>(url);
  }

  // Email Deliverability Guardian & Mailbox Warmup (Category 01)
  public checkDomainDns(payload: any): Observable<DomainDnsHealthRecordDto> {
    return this.http.post<DomainDnsHealthRecordDto>(`${this.baseUrl}/deliverability/dns-check`, payload);
  }

  public analyzeSpamRisk(payload: any): Observable<EmailSpamAnalysisReportDto> {
    return this.http.post<EmailSpamAnalysisReportDto>(`${this.baseUrl}/deliverability/spam-analyze`, payload);
  }

  public startWarmup(payload: any): Observable<MailboxWarmupCadenceDto> {
    return this.http.post<MailboxWarmupCadenceDto>(`${this.baseUrl}/deliverability/warmup/start`, payload);
  }

  public getWarmup(tenantId: string, senderEmail: string): Observable<MailboxWarmupCadenceDto> {
    return this.http.get<MailboxWarmupCadenceDto>(`${this.baseUrl}/deliverability/warmup?tenantId=${tenantId}&senderEmail=${encodeURIComponent(senderEmail)}`);
  }

  public simulateWarmupStep(cadenceId: string, tenantId: string): Observable<MailboxWarmupCadenceDto> {
    return this.http.post<MailboxWarmupCadenceDto>(`${this.baseUrl}/deliverability/warmup/${cadenceId}/simulate-step?tenantId=${tenantId}`, {});
  }

  public getWarmupLogs(cadenceId: string, tenantId: string): Observable<WarmupExchangeLogDto[]> {
    return this.http.get<WarmupExchangeLogDto[]>(`${this.baseUrl}/deliverability/warmup/${cadenceId}/logs?tenantId=${tenantId}`);
  }

  // Product Telemetry & Live Visitor Analytics (Category 14)
  public ingestTelemetry(payload: any): Observable<TelemetryEventDto> {
    return this.http.post<TelemetryEventDto>(`${this.baseUrl}/telemetry/events`, payload);
  }

  public getLiveTelemetry(tenantId: string, windowMinutes = 15): Observable<LiveTelemetrySummaryDto> {
    return this.http.get<LiveTelemetrySummaryDto>(`${this.baseUrl}/telemetry/live?tenantId=${tenantId}&windowMinutes=${windowMinutes}`);
  }

  public getRecentTelemetryEvents(tenantId: string, limit = 50): Observable<TelemetryEventDto[]> {
    return this.http.get<TelemetryEventDto[]>(`${this.baseUrl}/telemetry/events/recent?tenantId=${tenantId}&limit=${limit}`);
  }

  // SaaS Subscription Billing & Plan Quotas (Section 11)
  public getSubscriptionDetails(tenantId: string): Observable<SubscriptionDetailsDto> {
    return this.http.get<SubscriptionDetailsDto>(`${this.baseUrl}/billing/subscription?tenantId=${tenantId}`);
  }

  public upgradePlan(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/billing/upgrade`, payload);
  }

  public checkQuota(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/billing/quota-check`, payload);
  }

  public getInvoices(tenantId: string): Observable<BillingInvoiceRecordDto[]> {
    return this.http.get<BillingInvoiceRecordDto[]>(`${this.baseUrl}/billing/invoices?tenantId=${tenantId}`);
  }

  public createCheckoutSession(payload: any): Observable<{ checkoutUrl: string }> {
    return this.http.post<{ checkoutUrl: string }>(`${this.baseUrl}/billing/checkout-session`, payload);
  }

  // Frontier 2: Digital Goods, Downloads, Licenses & PIM Variant Matrix
  public getDigitalAssets(tenantId: string): Observable<DigitalAssetDto[]> {
    return this.http.get<DigitalAssetDto[]>(`${this.baseUrl}/digital/assets?tenantId=${tenantId}`);
  }

  public createDigitalAsset(payload: any): Observable<DigitalAssetDto> {
    return this.http.post<DigitalAssetDto>(`${this.baseUrl}/digital/assets`, payload);
  }

  public issueDownloadToken(payload: any): Observable<DigitalDownloadTokenDto> {
    return this.http.post<DigitalDownloadTokenDto>(`${this.baseUrl}/digital/tokens/issue`, payload);
  }

  public getLicenseKeys(tenantId: string, sku?: string): Observable<ProductLicenseKeyDto[]> {
    let url = `${this.baseUrl}/digital/licenses?tenantId=${tenantId}`;
    if (sku) url += `&sku=${encodeURIComponent(sku)}`;
    return this.http.get<ProductLicenseKeyDto[]>(url);
  }

  public generateLicenseKeys(payload: any): Observable<ProductLicenseKeyDto[]> {
    return this.http.post<ProductLicenseKeyDto[]>(`${this.baseUrl}/digital/licenses`, payload);
  }

  public generateVariantMatrix(payload: any): Observable<PimVariantMatrixDto[]> {
    return this.http.post<PimVariantMatrixDto[]>(`${this.baseUrl}/digital/variants/matrix-generate`, payload);
  }

  public getVariantMatrix(tenantId: string, baseSku: string): Observable<PimVariantMatrixDto[]> {
    return this.http.get<PimVariantMatrixDto[]>(`${this.baseUrl}/digital/variants/matrix?tenantId=${tenantId}&baseSku=${encodeURIComponent(baseSku)}`);
  }

  // Frontier 3: Dynamic Forms & Cryptographic PDF Seal Registry
  public getForms(tenantId: string): Observable<DynamicFormDefinitionDto[]> {
    return this.http.get<DynamicFormDefinitionDto[]>(`${this.baseUrl}/forms?tenantId=${tenantId}`);
  }

  public createForm(payload: any): Observable<DynamicFormDefinitionDto> {
    return this.http.post<DynamicFormDefinitionDto>(`${this.baseUrl}/forms`, payload);
  }

  public getFormBySlug(tenantId: string, slug: string): Observable<DynamicFormDefinitionDto> {
    return this.http.get<DynamicFormDefinitionDto>(`${this.baseUrl}/forms/${slug}/public?tenantId=${tenantId}`);
  }

  public submitFormAnswers(slug: string, payload: any): Observable<DynamicFormSubmissionDto> {
    return this.http.post<DynamicFormSubmissionDto>(`${this.baseUrl}/forms/${slug}/submit`, payload);
  }

  public getSubmissions(tenantId: string, formId: string): Observable<DynamicFormSubmissionDto[]> {
    return this.http.get<DynamicFormSubmissionDto[]>(`${this.baseUrl}/forms/${formId}/submissions?tenantId=${tenantId}`);
  }

  public createDocument(payload: any): Observable<CryptographicPdfRecordDto> {
    return this.http.post<CryptographicPdfRecordDto>(`${this.baseUrl}/documents/create`, payload);
  }

  public sealDocument(payload: any): Observable<CryptographicPdfRecordDto> {
    return this.http.post<CryptographicPdfRecordDto>(`${this.baseUrl}/documents/seal`, payload);
  }

  public verifyDocumentAuditChain(tenantId: string, documentId: string): Observable<{ isTamperFree: boolean; summary: string }> {
    return this.http.get<{ isTamperFree: boolean; summary: string }>(`${this.baseUrl}/documents/${documentId}/verify-audit-chain?tenantId=${tenantId}`);
  }

  // Frontier 4: Developer API Keys & Outbound Webhooks Mesh
  public getApiKeys(tenantId: string): Observable<TenantApiKeyDto[]> {
    return this.http.get<TenantApiKeyDto[]>(`${this.baseUrl}/developer/api-keys?tenantId=${tenantId}`);
  }

  public createApiKey(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/developer/api-keys`, payload);
  }

  public revokeApiKey(tenantId: string, keyId: string): Observable<any> {
    return this.http.delete<any>(`${this.baseUrl}/developer/api-keys/${keyId}?tenantId=${tenantId}`);
  }

  public getWebhookSubscriptions(tenantId: string): Observable<WebhookSubscriptionRecordDto[]> {
    return this.http.get<WebhookSubscriptionRecordDto[]>(`${this.baseUrl}/developer/webhooks/subscriptions?tenantId=${tenantId}`);
  }

  public createWebhookSubscription(payload: any): Observable<WebhookSubscriptionRecordDto> {
    return this.http.post<WebhookSubscriptionRecordDto>(`${this.baseUrl}/developer/webhooks/subscriptions`, payload);
  }

  public toggleWebhookSubscription(tenantId: string, subId: string, isActive: boolean): Observable<any> {
    return this.http.put<any>(`${this.baseUrl}/developer/webhooks/subscriptions/${subId}/toggle`, { tenantId, isActive });
  }

  public getWebhookLogs(tenantId: string, subscriptionId?: string, limit = 50): Observable<WebhookDeliveryAttemptLogDto[]> {
    let url = `${this.baseUrl}/developer/webhooks/logs?tenantId=${tenantId}&limit=${limit}`;
    if (subscriptionId) url += `&subscriptionId=${subscriptionId}`;
    return this.http.get<WebhookDeliveryAttemptLogDto[]>(url);
  }

  public testDispatchWebhook(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/developer/webhooks/test-dispatch`, payload);
  }

  public retryWebhookDelivery(tenantId: string, logId: string): Observable<WebhookDeliveryAttemptLogDto> {
    return this.http.post<WebhookDeliveryAttemptLogDto>(`${this.baseUrl}/developer/webhooks/logs/${logId}/retry?tenantId=${tenantId}`, {});
  }

  // Horizon 1: Mobile Courier Companion & Headless COD Checkout
  public startDriverSession(payload: any): Observable<DriverMobileSessionDto> {
    return this.http.post<DriverMobileSessionDto>(`${this.baseUrl}/commerce/courier/session`, payload);
  }

  public getRunSheetStops(runSheetId: string, tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/commerce/courier/run-sheets/${runSheetId}/stops?tenantId=${tenantId}`);
  }

  public submitDoorstepPod(payload: any): Observable<DoorstepPodDto> {
    return this.http.post<DoorstepPodDto>(`${this.baseUrl}/commerce/courier/pod`, payload);
  }

  public getDriverCashSummary(tenantId: string, driverId: string): Observable<DriverCashSummaryDto> {
    return this.http.get<DriverCashSummaryDto>(`${this.baseUrl}/commerce/courier/cash-summary?tenantId=${tenantId}&driverId=${driverId}`);
  }

  public pingDriverGps(payload: any): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/commerce/courier/gps-heartbeat`, payload);
  }

  public createCodCheckoutSession(payload: any): Observable<CodCheckoutSessionDto> {
    return this.http.post<CodCheckoutSessionDto>(`${this.baseUrl}/commerce/checkout/session`, payload);
  }

  public getCheckoutWilayas(countryCode = 'TN'): Observable<WilayaZoneDto[]> {
    return this.http.get<WilayaZoneDto[]>(`${this.baseUrl}/commerce/checkout/wilayas?countryCode=${countryCode}`);
  }

  public updateCheckoutDetails(payload: any): Observable<CodCheckoutSessionDto> {
    return this.http.post<CodCheckoutSessionDto>(`${this.baseUrl}/commerce/checkout/details`, payload);
  }

  public sendCheckoutOtp(payload: any): Observable<{ success: boolean; simulatedOtp: string }> {
    return this.http.post<{ success: boolean; simulatedOtp: string }>(`${this.baseUrl}/commerce/checkout/send-otp`, payload);
  }

  public verifyCheckoutOtp(payload: any): Observable<{ verified: boolean }> {
    return this.http.post<{ verified: boolean }>(`${this.baseUrl}/commerce/checkout/verify-otp`, payload);
  }

  public applyCheckoutUpsell(payload: any): Observable<CodCheckoutSessionDto> {
    return this.http.post<CodCheckoutSessionDto>(`${this.baseUrl}/commerce/checkout/apply-upsell`, payload);
  }

  public completeCheckout(payload: any): Observable<{ success: boolean; orderId: string; orderNumber: string; totalAmount: number; message: string }> {
    return this.http.post<{ success: boolean; orderId: string; orderNumber: string; totalAmount: number; message: string }>(`${this.baseUrl}/commerce/checkout/complete`, payload);
  }

  // Horizon 3: UGC Creative Generator & Competitor Intelligence Radar
  public generateUgcScripts(payload: any): Observable<UgcVideoScriptDto[]> {
    return this.http.post<UgcVideoScriptDto[]>(`${this.baseUrl}/marketing/ugc/generate-scripts`, payload);
  }

  public getUgcScripts(tenantId: string, sku: string): Observable<UgcVideoScriptDto[]> {
    return this.http.get<UgcVideoScriptDto[]>(`${this.baseUrl}/marketing/ugc/scripts?tenantId=${tenantId}&sku=${sku}`);
  }

  public ingestCompetitorAd(payload: any): Observable<CompetitorAdRecordDto> {
    return this.http.post<CompetitorAdRecordDto>(`${this.baseUrl}/marketing/competitor/ad`, payload);
  }

  public trackCompetitorPrice(payload: any): Observable<CompetitorPriceTrackerDto> {
    return this.http.post<CompetitorPriceTrackerDto>(`${this.baseUrl}/marketing/competitor/price`, payload);
  }

  public getCompetitorRadar(tenantId: string): Observable<CompetitorRadarSummaryDto> {
    return this.http.get<CompetitorRadarSummaryDto>(`${this.baseUrl}/marketing/competitor/radar?tenantId=${tenantId}`);
  }

  // Horizon 4: Enterprise B2B Financials & Factoring
  public establishB2BCredit(payload: any): Observable<B2BCreditAccountDto> {
    return this.http.post<B2BCreditAccountDto>(`${this.baseUrl}/billing/b2b-credit/accounts`, payload);
  }

  public recordInvoiceDrawdown(payload: any): Observable<B2BCreditAccountDto> {
    return this.http.post<B2BCreditAccountDto>(`${this.baseUrl}/billing/b2b-credit/invoice-drawdown`, payload);
  }

  public recordPaymentSettlement(payload: any): Observable<B2BCreditAccountDto> {
    return this.http.post<B2BCreditAccountDto>(`${this.baseUrl}/billing/b2b-credit/settlement`, payload);
  }

  public requestCourierFactoring(payload: any): Observable<FactoringReceivableRecordDto> {
    return this.http.post<FactoringReceivableRecordDto>(`${this.baseUrl}/billing/factoring/request`, payload);
  }

  public fundFactoringAdvance(factoringId: string, tenantId: string): Observable<FactoringReceivableRecordDto> {
    return this.http.post<FactoringReceivableRecordDto>(`${this.baseUrl}/billing/factoring/fund/${factoringId}?tenantId=${tenantId}`, {});
  }

  public getB2BFinancialSummary(tenantId: string): Observable<{ creditAccounts: B2BCreditAccountDto[]; factoringReceivables: FactoringReceivableRecordDto[]; totalFactoringAvailable: number; totalCreditExposure: number }> {
    return this.http.get<{ creditAccounts: B2BCreditAccountDto[]; factoringReceivables: FactoringReceivableRecordDto[]; totalFactoringAvailable: number; totalCreditExposure: number }>(`${this.baseUrl}/billing/b2b-financials/summary?tenantId=${tenantId}`);
  }

  public replayDeveloperWebhook(attemptId: string, tenantId: string): Observable<WebhookDeliveryAttemptLogDto> {
    return this.http.post<WebhookDeliveryAttemptLogDto>(`${this.baseUrl}/developer/webhooks/replay/${attemptId}?tenantId=${tenantId}`, {});
  }

  // Social Publishing & Comments Deflector
  public getSocialChannels(): Observable<SocialChannelMetadataDto[]> {
    return this.http.get<SocialChannelMetadataDto[]>(`${this.baseUrl}/social/channels`);
  }

  public getSocialAccounts(tenantId: string): Observable<SocialAccountDto[]> {
    return this.http.get<SocialAccountDto[]>(`${this.baseUrl}/social/accounts?tenantId=${tenantId}`);
  }

  public connectSocialAccount(payload: any): Observable<SocialAccountDto> {
    return this.http.post<SocialAccountDto>(`${this.baseUrl}/social/accounts`, payload);
  }

  public getSocialPosts(tenantId: string): Observable<SocialPostDto[]> {
    return this.http.get<SocialPostDto[]>(`${this.baseUrl}/social/posts?tenantId=${tenantId}`);
  }

  public createSocialPost(payload: any): Observable<SocialPostDto> {
    return this.http.post<SocialPostDto>(`${this.baseUrl}/social/posts`, payload);
  }

  public publishDueSocialPosts(tenantId: string): Observable<{ count: number; publishedPosts: SocialPostDto[] }> {
    return this.http.post<{ count: number; publishedPosts: SocialPostDto[] }>(`${this.baseUrl}/social/posts/publish-due?tenantId=${tenantId}`, {});
  }

  public publishSocialPostNow(postId: string, tenantId: string): Observable<{ success: boolean; post: SocialPostDto }> {
    return this.http.post<{ success: boolean; post: SocialPostDto }>(`${this.baseUrl}/social/posts/${postId}/publish-now?tenantId=${tenantId}`, {});
  }

  public deflectSocialComment(payload: any): Observable<DeflectCommentResultDto> {
    return this.http.post<DeflectCommentResultDto>(`${this.baseUrl}/social/deflect`, payload);
  }

  // Calendar Public Slug, Reschedule & Cancel
  public getPublicBookingPage(tenantSlug: string, eventSlug: string, date?: string): Observable<{ eventType: BookingEventTypeDto; slots: TimeSlotDto[] }> {
    let url = `${this.baseUrl}/calendar/public/${tenantSlug}/${eventSlug}`;
    if (date) url += `?date=${date}`;
    return this.http.get<{ eventType: BookingEventTypeDto; slots: TimeSlotDto[] }>(url);
  }

  public bookPublicAppointment(tenantSlug: string, eventSlug: string, payload: any): Observable<CalendarBookingRecordDto> {
    return this.http.post<CalendarBookingRecordDto>(`${this.baseUrl}/calendar/public/${tenantSlug}/${eventSlug}/book`, payload);
  }

  public cancelCalendarBooking(bookingId: string, tenantId: string, reason = ''): Observable<CalendarBookingRecordDto> {
    return this.http.post<CalendarBookingRecordDto>(`${this.baseUrl}/calendar/bookings/${bookingId}/cancel`, { tenantId, reason });
  }

  public rescheduleCalendarBooking(bookingId: string, tenantId: string, newStartTimeUtc: string): Observable<CalendarBookingRecordDto> {
    return this.http.post<CalendarBookingRecordDto>(`${this.baseUrl}/calendar/bookings/${bookingId}/reschedule`, { tenantId, newStartTimeUtc });
  }

  public getEventHosts(tenantId: string, eventTypeId: string): Observable<HostWeightDto[]> {
    return this.http.get<HostWeightDto[]>(`${this.baseUrl}/calendar/event-types/${eventTypeId}/hosts?tenantId=${tenantId}`);
  }

  public updateHostWeights(tenantId: string, eventTypeId: string, weights: Record<string, number>): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(`${this.baseUrl}/calendar/event-types/${eventTypeId}/hosts/weights`, {
      tenantId,
      weights
    });
  }

  // SEO & Generative Engine Optimization (GEO)
  public computeGeoScore(tenantId: string, brandName: string, domainUrl: string): Observable<GeoScoreReportDto> {
    return this.http.post<GeoScoreReportDto>(`${this.baseUrl}/seo/geo/score`, { tenantId, brandName, domainUrl });
  }

  // SDR Power Dialer & Outbound Calling
  public getDialerQueue(tenantId: string): Observable<DialerQueueItemDto[]> {
    return this.http.get<DialerQueueItemDto[]>(`${this.baseUrl}/sdr/dialer/queue?tenantId=${tenantId}`);
  }

  public logCallOutcome(payload: any): Observable<CallOutcomeResultDto> {
    return this.http.post<CallOutcomeResultDto>(`${this.baseUrl}/sdr/dialer/call-outcome`, payload);
  }

  // Customer Journeys (dittofeed model)
  public createCustomerJourney(payload: any): Observable<CustomerJourneyDto> {
    return this.http.post<CustomerJourneyDto>(`${this.baseUrl}/journeys`, payload);
  }

  public getJourneySteps(journeyId: string, tenantId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.baseUrl}/journeys/${journeyId}/steps?tenantId=${tenantId}`);
  }

  public toggleJourneyStatus(journeyId: string, tenantId: string): Observable<{ status: string; journey: CustomerJourneyDto }> {
    return this.http.post<{ status: string; journey: CustomerJourneyDto }>(`${this.baseUrl}/journeys/${journeyId}/toggle?tenantId=${tenantId}`, {});
  }

  public simulateJourneyEvent(payload: any): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/journeys/simulate`, payload);
  }

  // ==========================================
  // INSPIRATION STACK CAPABILITIES (V1 APIS)
  // ==========================================

  // 1. Chatbot Flow Builder & Instagram/FB Comment-to-DM Engine
  public registerChatbotFlow(payload: { name: string; channel: string; nodes: ChatbotFlowNodeDto[]; entryNodeId: string }, tenantId?: string): Observable<ChatbotFlowDefinitionDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<ChatbotFlowDefinitionDto>(`${this.baseUrl}/v1/funnels/chatbot/flows`, payload, { headers });
  }

  public executeChatbotFlowStep(flowId: string, payload: { currentNodeId?: string; variables?: Record<string, any>; userResponse?: string }, tenantId?: string): Observable<FlowExecutionResultDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<FlowExecutionResultDto>(`${this.baseUrl}/v1/funnels/chatbot/flows/${flowId}/execute`, payload, { headers });
  }

  public processSocialCommentWebhook(payload: any, tenantId?: string): Observable<SocialCommentReactionDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<SocialCommentReactionDto>(`${this.baseUrl}/v1/funnels/chatbot/comments/webhook`, payload, { headers });
  }

  // 2. In-App Product Tours & Smart Context Banners
  public createInAppTour(payload: any, tenantId?: string): Observable<InAppTourDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<InAppTourDto>(`${this.baseUrl}/v1/engagement/tours`, payload, { headers });
  }

  public getActiveTours(path = '/', tenantId?: string): Observable<InAppTourDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<InAppTourDto[]>(`${this.baseUrl}/v1/engagement/tours/active?path=${encodeURIComponent(path)}`, { headers });
  }

  public completeInAppTour(tourId: string, user: string, tenantId?: string): Observable<{ tourId: string; completed: boolean }> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<{ tourId: string; completed: boolean }>(`${this.baseUrl}/v1/engagement/tours/${tourId}/complete?user=${encodeURIComponent(user)}`, {}, { headers });
  }

  public createInAppBanner(payload: any, tenantId?: string): Observable<InAppBannerDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<InAppBannerDto>(`${this.baseUrl}/v1/engagement/banners`, payload, { headers });
  }

  public evaluateActiveBanners(path = '/', segment = 'ALL', tenantId?: string): Observable<InAppBannerDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<InAppBannerDto[]>(`${this.baseUrl}/v1/engagement/banners/evaluate?path=${encodeURIComponent(path)}&segment=${encodeURIComponent(segment)}`, { headers });
  }

  // 3. Multi-Operator Collision Prevention, SLA Watches & CSAT
  public updateOperatorPresence(conversationId: string, payload: { operatorId: string; operatorName: string; isTyping: boolean }, tenantId?: string): Observable<OperatorPresenceDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<OperatorPresenceDto>(`${this.baseUrl}/v1/conversations/${conversationId}/presence`, payload, { headers });
  }

  public getActiveViewers(conversationId: string, tenantId?: string): Observable<OperatorPresenceDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<OperatorPresenceDto[]>(`${this.baseUrl}/v1/conversations/${conversationId}/presence`, { headers });
  }

  public getSlaBreachAlerts(tenantId?: string): Observable<SlaBreachAlertDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<SlaBreachAlertDto[]>(`${this.baseUrl}/v1/conversations/sla/breaches`, { headers });
  }

  public submitConversationCsat(conversationId: string, ratingScore: number, comment?: string, tenantId?: string): Observable<CsatFeedbackDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<CsatFeedbackDto>(`${this.baseUrl}/v1/conversations/${conversationId}/csat`, { ratingScore, comment }, { headers });
  }

  // 4. Peer-to-Peer Warmup & Spam Rescue Sweeper
  public pairWarmupPeers(tenantId?: string): Observable<WarmupPeerPairDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<WarmupPeerPairDto[]>(`${this.baseUrl}/v1/deliverability/warmup/peers/pair`, {}, { headers });
  }

  public runSpamRescueSweep(tenantId?: string): Observable<SpamRescueSummaryDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<SpamRescueSummaryDto>(`${this.baseUrl}/v1/deliverability/warmup/spam-rescue/run`, {}, { headers });
  }

  public getDnsDiagnostics(domain: string): Observable<DnsDeliverabilityReportDto> {
    return this.http.get<DnsDeliverabilityReportDto>(`${this.baseUrl}/v1/deliverability/warmup/dns-diagnostics/${encodeURIComponent(domain)}`);
  }

  // 5. Autonomous SDR Fleet & WebRTC Softphone Calling
  public allocateInboxSlots(volume = 100, tenantId?: string): Observable<InboxSlotAllocationDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<InboxSlotAllocationDto[]>(`${this.baseUrl}/v1/outbound/sdr/inbox-rotation/allocate?volume=${volume}`, {}, { headers });
  }

  public startWebRtcCallSession(prospectPhone: string, agentPhone: string, tenantId?: string): Observable<CallSessionInitiationDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<CallSessionInitiationDto>(`${this.baseUrl}/v1/outbound/sdr/calls/session`, { prospectPhone, agentPhone }, { headers });
  }

  public submitCallDisposition(payload: { callSessionId: string; dispositionOutcome: string; notes?: string; callbackScheduledAtUtc?: string }, tenantId?: string): Observable<{ success: boolean }> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<{ success: boolean }>(`${this.baseUrl}/v1/outbound/sdr/calls/disposition`, payload, { headers });
  }

  public classifyInboundReply(subject: string, body: string, tenantId?: string): Observable<ClassifiedReplyDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<ClassifiedReplyDto>(`${this.baseUrl}/v1/outbound/sdr/replies/classify`, { subject, body }, { headers });
  }

  // 6. Durable Multi-Platform Publishing & AI Viral Hooks
  public publishPostDurably(postId: string, platforms: number[], tenantId?: string): Observable<DurablePublishResultDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<DurablePublishResultDto[]>(`${this.baseUrl}/v1/social/posts/publish-durable`, { postId, platforms }, { headers });
  }

  public generateViralHooks(topic = 'B2B SaaS Growth', tenantId?: string): Observable<ViralHookSuggestionDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<ViralHookSuggestionDto[]>(`${this.baseUrl}/v1/social/ai/viral-hooks?topic=${encodeURIComponent(topic)}`, {}, { headers });
  }

  public rewritePostForPlatforms(masterDraft: string, platforms: number[], tenantId?: string, preferredAspectRatio?: string): Observable<PlatformAdaptedPostDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<PlatformAdaptedPostDto[]>(`${this.baseUrl}/v1/social/ai/rewrite`, { masterDraft, platforms, preferredAspectRatio }, { headers });
  }

  public auditSocialTokenHealth(tenantId?: string): Observable<TokenHealthAuditReportDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<TokenHealthAuditReportDto>(`${this.baseUrl}/v1/social/accounts/audit-tokens`, {}, { headers });
  }

  // 7. Model Context Protocol (MCP) & Autonomous Fact Extraction
  public getMcpTools(): Observable<McpToolDefinitionDto[]> {
    return this.http.get<McpToolDefinitionDto[]>(`${this.baseUrl}/v1/mcp/tools`);
  }

  public executeMcpTool(toolName: string, args: Record<string, any>, tenantId?: string): Observable<any> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<any>(`${this.baseUrl}/v1/mcp/tools/${encodeURIComponent(toolName)}/invoke`, args, { headers });
  }

  public extractContactFacts(contactId: string, transcript: string, tenantId?: string): Observable<ContactFactDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<ContactFactDto[]>(`${this.baseUrl}/v1/mcp/contacts/${contactId}/extract-facts`, { transcript }, { headers });
  }

  public getContactBrief(contactId: string, tenantId?: string): Observable<ContactBriefDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<ContactBriefDto>(`${this.baseUrl}/v1/mcp/contacts/${contactId}/brief`, { headers });
  }

  // 8. Advanced CRM (Frappe-inspired Rotten Deals, Territories, DocTypes)
  public getRottenDeals(thresholdDays = 7, tenantId?: string): Observable<RottenDealAlertDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<RottenDealAlertDto[]>(`${this.baseUrl}/v1/crm/advanced/deals/rotten?thresholdDays=${thresholdDays}`, { headers });
  }

  public validateDocTypeSchema(docTypeName: string, fields: Record<string, any>, tenantId?: string): Observable<any> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<any>(`${this.baseUrl}/v1/crm/advanced/doctypes/validate`, { docTypeName, fields }, { headers });
  }

  public assignLeadTerritory(payload: { leadId: string; leadAddress: string; latitude: number; longitude: number }, tenantId?: string): Observable<any> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<any>(`${this.baseUrl}/v1/crm/advanced/territories/assign`, payload, { headers });
  }

  // 9. Helpdesk Commercial ERP Context & Pause-on-Hold SLAs
  public getTicketCommercialContext(ticketId: string, tenantId?: string): Observable<TicketCommercialContextDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<TicketCommercialContextDto>(`${this.baseUrl}/v1/helpdesk/tickets/${ticketId}/erp-context`, { headers });
  }

  public setTicketSlaPauseState(ticketId: string, pause: boolean, reason: string, tenantId?: string): Observable<any> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<any>(`${this.baseUrl}/v1/helpdesk/tickets/${ticketId}/pause-sla`, { pause, reason }, { headers });
  }

  // 10. CPQ Quotes & Polymorphic EAV
  public createCpqQuote(payload: { customerName: string; customerEmail: string; currency: string; lineItems: CpqLineItemDto[]; globalDiscountPercentage: number }, tenantId?: string): Observable<CpqQuoteDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<CpqQuoteDto>(`${this.baseUrl}/v1/commerce/cpq/quotes`, payload, { headers });
  }

  public getCpqQuotePdfUrl(quoteId: string, tenantId?: string): string {
    const tenantParam = tenantId ? `?tenantId=${tenantId}` : '';
    return `${this.baseUrl}/v1/commerce/cpq/quotes/${quoteId}/pdf${tenantParam}`;
  }

  public setEavAttribute(payload: { entityType: string; entityId: string; key: string; value: string; dataType?: string }, tenantId?: string): Observable<EavAttributeDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<EavAttributeDto>(`${this.baseUrl}/v1/commerce/cpq/eav/attributes`, payload, { headers });
  }

  public getEavAttributes(entityType: string, entityId: string, tenantId?: string): Observable<EavAttributeDto[]> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<EavAttributeDto[]>(`${this.baseUrl}/v1/commerce/cpq/eav/attributes?entityType=${encodeURIComponent(entityType)}&entityId=${encodeURIComponent(entityId)}`, { headers });
  }

  // 11. Cryptographic Document Sealing & Documenso-grade Signatures
  public sealDocumentPkcs7(documentId: string, signerEmail: string, pdfBase64?: string, tenantId?: string): Observable<CryptographicSealResultDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<CryptographicSealResultDto>(`${this.baseUrl}/v1/documents/${documentId}/cryptographic-seal`, { pdfBase64, signerEmail }, { headers });
  }

  public getCompletionCertificate(documentId: string, tenantId?: string): Observable<CompletionCertificateDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<CompletionCertificateDto>(`${this.baseUrl}/v1/documents/${documentId}/completion-certificate`, { headers });
  }

  public recordDocumentPageDwell(documentId: string, pageNumber: number, secondsDwell: number, viewerIp?: string, viewerEmail?: string, maxScrollPercent?: number, tenantId?: string): Observable<any> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<any>(`${this.baseUrl}/v1/documents/${documentId}/telemetry/dwell`, {
      pageNumber,
      secondsDwell,
      viewerIp,
      viewerEmail,
      maxScrollPercent
    }, { headers });
  }

  public getDocumentTelemetry(documentId: string, tenantId?: string): Observable<DocumentTelemetrySummaryDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.get<DocumentTelemetrySummaryDto>(`${this.baseUrl}/v1/documents/${documentId}/telemetry`, { headers });
  }

  public verifyDocumentSeal(documentId: string, token?: string, expectedHash?: string, tenantId?: string): Observable<SealVerificationResultDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    const params: string[] = [];
    if (token) params.push(`token=${encodeURIComponent(token)}`);
    if (expectedHash) params.push(`expectedHash=${encodeURIComponent(expectedHash)}`);
    const qs = params.length > 0 ? `?${params.join('&')}` : '';
    return this.http.get<SealVerificationResultDto>(`${this.baseUrl}/v1/documents/${documentId}/verify-seal${qs}`, { headers });
  }

  public verifyDocumentSealWithBytes(documentId: string, token?: string, pdfBase64?: string, tenantId?: string): Observable<SealVerificationResultDto> {
    const headers = tenantId ? { 'X-Tenant-Id': tenantId } : undefined;
    return this.http.post<SealVerificationResultDto>(`${this.baseUrl}/v1/documents/${documentId}/verify-seal`, {
      verificationToken: token,
      pdfBase64
    }, { headers });
  }

  // 12. Seven Revenue Pillars
  public previewSpintax(template: string, prospectIdOrEmail?: string, variables?: Record<string, string>, sampleCount = 3): Observable<SpintaxPreviewResponse> {
    return this.http.post<SpintaxPreviewResponse>(`${this.baseUrl}/v1/outbound/spintax/preview`, {
      template,
      prospectIdOrEmail,
      variables,
      sampleCount
    });
  }

  public verifyEmailDeliverability(email: string): Observable<EmailVerificationResult> {
    return this.http.post<EmailVerificationResult>(`${this.baseUrl}/v1/outbound/email/verify?email=${encodeURIComponent(email)}`, {});
  }

  public enrichAndVerifyContact(tenantId: string, payload: any): Observable<EnrichedContactDto> {
    return this.http.post<EnrichedContactDto>(`${this.baseUrl}/v1/outbound/contacts/enrich-and-verify?tenantId=${tenantId}`, payload);
  }

  public generateWarmupThread(payload: any): Observable<WarmupDialogueThread> {
    return this.http.post<WarmupDialogueThread>(`${this.baseUrl}/v1/deliverability/warmup/threads/generate`, payload);
  }

  public runPlacementTest(payload: any): Observable<SeedPlacementReportDto> {
    return this.http.post<SeedPlacementReportDto>(`${this.baseUrl}/v1/deliverability/placement-test`, payload);
  }

  public parseMimeEmail(rawMime: string): Observable<ParsedMimeEmail> {
    return this.http.post<ParsedMimeEmail>(`${this.baseUrl}/v1/conversations/mime/parse`, { rawMime });
  }

  public getCannedMacros(tenantId: string, category?: string): Observable<CannedMacroDto[]> {
    const catQuery = category ? `&category=${encodeURIComponent(category)}` : '';
    return this.http.get<CannedMacroDto[]>(`${this.baseUrl}/v1/conversations/macros?tenantId=${tenantId}${catQuery}`);
  }

  public createCannedMacro(tenantId: string, payload: any): Observable<CannedMacroDto> {
    return this.http.post<CannedMacroDto>(`${this.baseUrl}/v1/conversations/macros?tenantId=${tenantId}`, payload);
  }

  public resolveCannedMacro(tenantId: string, triggerKey: string, context: any): Observable<CannedMacroResolutionDto> {
    return this.http.post<CannedMacroResolutionDto>(`${this.baseUrl}/v1/conversations/macros/resolve?tenantId=${tenantId}`, {
      triggerKey,
      context
    });
  }

  public triggerConversationCsat(conversationId: string, tenantId: string, channel = 'email'): Observable<CsatSurveyDispatchResult> {
    return this.http.post<CsatSurveyDispatchResult>(`${this.baseUrl}/v1/conversations/${conversationId}/csat/trigger?tenantId=${tenantId}`, { channel });
  }

  public evaluateSlaEscalations(tenantId: string): Observable<SlaEscalationEvaluationResult> {
    return this.http.post<SlaEscalationEvaluationResult>(`${this.baseUrl}/v1/conversations/sla/escalate?tenantId=${tenantId}`, {});
  }

  public previewCsvImport(csvContent: string, delimiter = ','): Observable<CsvImportPreviewResult> {
    return this.http.post<CsvImportPreviewResult>(`${this.baseUrl}/v1/crm/contacts/import/preview`, { csvContent, delimiter });
  }

  public executeCsvImport(tenantId: string, payload: any): Observable<CsvImportExecutionResult> {
    return this.http.post<CsvImportExecutionResult>(`${this.baseUrl}/v1/crm/contacts/import/execute?tenantId=${tenantId}`, payload);
  }

  public startBotSimulation(payload: any): Observable<BotSimulationSession> {
    return this.http.post<BotSimulationSession>(`${this.baseUrl}/v1/automation/bots/simulate/start`, payload);
  }

  public stepBotSimulation(payload: any): Observable<BotSimulationStepResult> {
    return this.http.post<BotSimulationStepResult>(`${this.baseUrl}/v1/automation/bots/simulate/step`, payload);
  }

  public createMultiPartyContract(tenantId: string, payload: any): Observable<MultiPartyContractDto> {
    return this.http.post<MultiPartyContractDto>(`${this.baseUrl}/v1/contracts/multi-party?tenantId=${tenantId}`, payload);
  }

  public startSigningWorkflow(contractId: string, tenantId: string): Observable<MultiPartyContractDto> {
    return this.http.post<MultiPartyContractDto>(`${this.baseUrl}/v1/contracts/${contractId}/workflow/start?tenantId=${tenantId}`, {});
  }

  public sendSignerOtp(signingToken: string, destination = 'email'): Observable<OtpVerificationResult> {
    return this.http.post<OtpVerificationResult>(`${this.baseUrl}/v1/contracts/signing/${signingToken}/otp/send`, { destination });
  }

  public verifySignerOtp(signingToken: string, otpCode: string): Observable<OtpVerificationResult> {
    return this.http.post<OtpVerificationResult>(`${this.baseUrl}/v1/contracts/signing/${signingToken}/otp/verify`, { otpCode });
  }

  public submitSignerSignature(signingToken: string, payload: any): Observable<MultiPartyContractDto> {
    return this.http.post<MultiPartyContractDto>(`${this.baseUrl}/v1/contracts/signing/${signingToken}/sign`, payload);
  }

  public generateCarrierWaybill(tenantId: string, payload: any): Observable<WaybillGenerationResult> {
    return this.http.post<WaybillGenerationResult>(`${this.baseUrl}/v1/commerce/shipping/waybill?tenantId=${tenantId}`, payload);
  }

  public trackCarrierShipment(tenantId: string, courierCode: string, trackingNumber: string): Observable<CarrierTrackingResult> {
    return this.http.get<CarrierTrackingResult>(`${this.baseUrl}/v1/commerce/shipping/track?tenantId=${tenantId}&courierCode=${encodeURIComponent(courierCode)}&trackingNumber=${encodeURIComponent(trackingNumber)}`);
  }

  public generateShippingLabel(tenantId: string, courierCode: string, trackingNumber: string, format = 0): Observable<ShippingLabelResult> {
    return this.http.post<ShippingLabelResult>(`${this.baseUrl}/v1/commerce/shipping/label?tenantId=${tenantId}&courierCode=${encodeURIComponent(courierCode)}&trackingNumber=${encodeURIComponent(trackingNumber)}&format=${format}`, {});
  }

  public evaluateCodFraud(tenantId: string, payload: any): Observable<CodFraudEvaluationResult> {
    return this.http.post<CodFraudEvaluationResult>(`${this.baseUrl}/v1/commerce/cod/evaluate-fraud?tenantId=${tenantId}`, payload);
  }

  // In-Browser WebRTC Video Consultation & In-Call AI Copilot (Calnode / DeskcommCRM / VoicEra)
  public getMeetingRoom(roomToken: string, tenantId?: string, hostName?: string, attendeeName?: string): Observable<VideoMeetingSessionDto> {
    let url = `${this.baseUrl}/v1/meetings/rooms/${encodeURIComponent(roomToken)}`;
    const params: string[] = [];
    if (tenantId) params.push(`tenantId=${tenantId}`);
    if (hostName) params.push(`hostName=${encodeURIComponent(hostName)}`);
    if (attendeeName) params.push(`attendeeName=${encodeURIComponent(attendeeName)}`);
    if (params.length > 0) url += `?${params.join('&')}`;
    return this.http.get<VideoMeetingSessionDto>(url);
  }

  public joinMeetingRoom(roomToken: string, payload: { participantIdentity: string; participantRole?: string; displayName?: string; isPublisher?: boolean }): Observable<{ token: WebRtcAccessTokenResultDto; room: VideoMeetingSessionDto }> {
    return this.http.post<{ token: WebRtcAccessTokenResultDto; room: VideoMeetingSessionDto }>(`${this.baseUrl}/v1/meetings/rooms/${encodeURIComponent(roomToken)}/join`, payload);
  }

  public updateMeetingMediaState(roomToken: string, payload: { participantIdentity: string; state: ParticipantMediaStateDto }): Observable<ParticipantStateDto> {
    return this.http.post<ParticipantStateDto>(`${this.baseUrl}/v1/meetings/rooms/${encodeURIComponent(roomToken)}/media-state`, payload);
  }

  public streamMeetingAudioChunk(roomToken: string, payload: { speakerIdentity: string; audioBase64?: string; transcriptHint?: string }): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/v1/meetings/rooms/${encodeURIComponent(roomToken)}/audio-stream`, payload);
  }

  public evaluateMeetingObjections(roomToken: string, transcriptSegment: string): Observable<ObjectionEvaluationResultDto> {
    return this.http.post<ObjectionEvaluationResultDto>(`${this.baseUrl}/v1/meetings/rooms/${encodeURIComponent(roomToken)}/battle-cards`, { transcriptSegment });
  }

  public endMeetingRoom(roomToken: string, summaryNotes = ''): Observable<MeetingSessionSummaryDto> {
    return this.http.post<MeetingSessionSummaryDto>(`${this.baseUrl}/v1/meetings/rooms/${encodeURIComponent(roomToken)}/end`, { summaryNotes });
  }

  // Multi-Inbox Sender Rotation & Deliverability Radar (Instantly / Warmbly / Postal)
  public getMultiInboxAccounts(tenantId?: string): Observable<MultiInboxAccountDto[]> {
    const url = tenantId ? `${this.baseUrl}/v1/deliverability/inboxes?tenantId=${tenantId}` : `${this.baseUrl}/v1/deliverability/inboxes`;
    return this.http.get<MultiInboxAccountDto[]>(url);
  }

  public registerSenderInbox(req: RegisterSenderInboxRequest): Observable<MultiInboxAccountDto> {
    return this.http.post<MultiInboxAccountDto>(`${this.baseUrl}/v1/deliverability/inboxes`, req);
  }

  public selectNextSender(recipientEmail: string, tenantId?: string): Observable<SelectNextSenderResponse> {
    return this.http.post<SelectNextSenderResponse>(`${this.baseUrl}/v1/deliverability/inboxes/select-next`, { recipientEmail, tenantId });
  }

  public quarantineSenderInbox(id: string, reason?: string, tenantId?: string): Observable<MultiInboxAccountDto> {
    const url = tenantId ? `${this.baseUrl}/v1/deliverability/inboxes/${id}/quarantine?tenantId=${tenantId}` : `${this.baseUrl}/v1/deliverability/inboxes/${id}/quarantine`;
    return this.http.post<MultiInboxAccountDto>(url, { reason, tenantId });
  }

  public thawSenderInbox(id: string, tenantId?: string): Observable<MultiInboxAccountDto> {
    const url = tenantId ? `${this.baseUrl}/v1/deliverability/inboxes/${id}/thaw?tenantId=${tenantId}` : `${this.baseUrl}/v1/deliverability/inboxes/${id}/thaw`;
    return this.http.post<MultiInboxAccountDto>(url, { tenantId });
  }

  public getDeliverabilityRadar(tenantId?: string): Observable<DeliverabilityRadarMetricsDto> {
    const url = tenantId ? `${this.baseUrl}/v1/deliverability/inboxes/radar?tenantId=${tenantId}` : `${this.baseUrl}/v1/deliverability/inboxes/radar`;
    return this.http.get<DeliverabilityRadarMetricsDto>(url);
  }

  public recordInboxDispatchEvent(id: string, payload: { wasDelivered: boolean; wasBounced: boolean; wasReplied: boolean; wasOpened?: boolean; isSpamComplaint?: boolean }): Observable<MultiInboxAccountDto> {
    return this.http.post<MultiInboxAccountDto>(`${this.baseUrl}/v1/deliverability/inboxes/${id}/dispatch-event`, payload);
  }

  public simulateBatchRotation(req: { tenantId?: string; batchSize?: number; googleRatio?: number; microsoftRatio?: number; customRatio?: number }): Observable<RotationSimulationResultDto> {
    return this.http.post<RotationSimulationResultDto>(`${this.baseUrl}/v1/deliverability/inboxes/simulate`, req);
  }
}
