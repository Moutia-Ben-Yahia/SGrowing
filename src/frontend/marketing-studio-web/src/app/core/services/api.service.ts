import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ContactDto, ConversationDto, MessageDto } from '../models/chat.models';
import { SenderAccountDto, CampaignDto, DealDto } from '../models/outbound.models';
import { CustomerJourneyDto, SurveyFormDto, SurveyResponseDto, HelpArticleDto, RagDeflectionResultDto } from '../models/platform.models';

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
}
