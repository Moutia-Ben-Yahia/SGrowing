import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AiDraftDto, ConversationDto, MessageDto } from '../models/chat.models';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection?: signalR.HubConnection;

  public isConnected = signal<boolean>(false);
  public messageReceived$ = new Subject<MessageDto>();
  public conversationUpdated$ = new Subject<ConversationDto>();
  public aiDraftGenerated$ = new Subject<AiDraftDto>();
  public typingIndicator$ = new Subject<{ conversationId: string; senderName: string; isTyping: boolean }>();

  public async startConnection(hubUrl = 'http://localhost:5000/hubs/omni'): Promise<void> {
    try {
      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          skipNegotiation: false,
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
        })
        .withAutomaticReconnect()
        .build();

      this.hubConnection.on('OnMessageReceived', (msg: MessageDto) => {
        this.messageReceived$.next(msg);
      });

      this.hubConnection.on('OnConversationUpdated', (conv: ConversationDto) => {
        this.conversationUpdated$.next(conv);
      });

      this.hubConnection.on('OnAiDraftGenerated', (draft: AiDraftDto) => {
        this.aiDraftGenerated$.next(draft);
      });

      this.hubConnection.on('OnTypingIndicator', (conversationId: string, senderName: string, isTyping: boolean) => {
        this.typingIndicator$.next({ conversationId, senderName, isTyping });
      });

      await this.hubConnection.start();
      this.isConnected.set(true);
      console.log('SignalR OmniHub connected successfully.');
    } catch (err) {
      console.warn('SignalR connection failed (running in offline/mock mode):', err);
      this.isConnected.set(false);
    }
  }

  public async joinConversation(conversationId: string): Promise<void> {
    if (this.hubConnection && this.isConnected()) {
      await this.hubConnection.invoke('JoinConversation', conversationId);
    }
  }

  public async leaveConversation(conversationId: string): Promise<void> {
    if (this.hubConnection && this.isConnected()) {
      await this.hubConnection.invoke('LeaveConversation', conversationId);
    }
  }

  public async broadcastTyping(conversationId: string, senderName: string, isTyping: boolean): Promise<void> {
    if (this.hubConnection && this.isConnected()) {
      await this.hubConnection.invoke('BroadcastTyping', conversationId, senderName, isTyping);
    }
  }
}
