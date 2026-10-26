import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ContactDto, ConversationDto, MessageDto } from '../models/chat.models';
import { SenderAccountDto, CampaignDto, DealDto } from '../models/outbound.models';
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
}
