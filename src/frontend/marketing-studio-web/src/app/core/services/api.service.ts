import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ContactDto, ConversationDto, MessageDto } from '../models/chat.models';

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
}
