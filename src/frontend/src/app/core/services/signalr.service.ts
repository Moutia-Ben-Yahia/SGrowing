import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AiDraftDto, ConversationDto, MessageDto } from '../models/chat.models';

export interface BufferedMessage {
  clientUuid: string;
  conversationId: string;
  content: string;
  queuedAt: string;
}

export type ConnectionState = 'Connected' | 'Reconnecting' | 'Disconnected';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection?: signalR.HubConnection;
  private readonly reconnectDelays = [0, 2000, 5000, 10000, 30000];

  public isConnected = signal<boolean>(false);
  public connectionState = signal<ConnectionState>('Disconnected');
  public offlineQueue = signal<BufferedMessage[]>([]);

  public messageReceived$ = new Subject<MessageDto>();
  public conversationUpdated$ = new Subject<ConversationDto>();
  public aiDraftGenerated$ = new Subject<AiDraftDto>();
  public typingIndicator$ = new Subject<{ conversationId: string; senderName: string; isTyping: boolean }>();
  public conversationLocked$ = new Subject<{ conversationId: string; lockedByUserId: string; agentName: string }>();
  public conversationUnlocked$ = new Subject<{ conversationId: string }>();
  public operatorPresenceChanged$ = new Subject<{ conversationId: string; operatorId: string; operatorName: string; isTyping: boolean }>();
  public slaBreachAlert$ = new Subject<{ conversationId: string; priority: string; minutesRemaining: number; isBreached: boolean }>();
  public callSessionStateChanged$ = new Subject<{ callSessionId: string; status: string; callerPhone?: string }>();
  public offlineMessageFlushed$ = new Subject<BufferedMessage>();

  public async startConnection(hubUrl = 'http://localhost:5000/hubs/omni'): Promise<void> {
    try {
      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          skipNegotiation: false,
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
        })
        .withAutomaticReconnect(this.reconnectDelays)
        .build();

      this.hubConnection.onreconnecting((error) => {
        console.warn('SignalR reconnecting...', error);
        this.isConnected.set(false);
        this.connectionState.set('Reconnecting');
      });

      this.hubConnection.onreconnected((connectionId) => {
        console.log('SignalR reconnected. Connection ID:', connectionId);
        this.isConnected.set(true);
        this.connectionState.set('Connected');
        void this.flushOfflineQueue();
      });

      this.hubConnection.onclose((error) => {
        console.warn('SignalR connection closed:', error);
        this.isConnected.set(false);
        this.connectionState.set('Disconnected');
      });

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

      this.hubConnection.on('OnConversationLocked', (conversationId: string, lockedByUserId: string, agentName: string) => {
        this.conversationLocked$.next({ conversationId, lockedByUserId, agentName });
      });

      this.hubConnection.on('OnConversationUnlocked', (conversationId: string) => {
        this.conversationUnlocked$.next({ conversationId });
      });

      this.hubConnection.on('OnOperatorPresenceChanged', (conversationId: string, operatorId: string, operatorName: string, isTyping: boolean) => {
        this.operatorPresenceChanged$.next({ conversationId, operatorId, operatorName, isTyping });
      });

      this.hubConnection.on('OnSlaBreachAlert', (conversationId: string, priority: string, minutesRemaining: number, isBreached: boolean) => {
        this.slaBreachAlert$.next({ conversationId, priority, minutesRemaining, isBreached });
      });

      this.hubConnection.on('OnCallSessionStateChanged', (callSessionId: string, status: string, callerPhone?: string) => {
        this.callSessionStateChanged$.next({ callSessionId, status, callerPhone });
      });

      await this.hubConnection.start();
      this.isConnected.set(true);
      this.connectionState.set('Connected');
      console.log('SignalR OmniHub connected successfully.');
      void this.flushOfflineQueue();
    } catch (err) {
      console.warn('SignalR connection failed (running in offline/mock mode):', err);
      this.isConnected.set(false);
      this.connectionState.set('Disconnected');
    }
  }

  public enqueueOfflineMessage(msg: BufferedMessage): void {
    this.offlineQueue.update(queue => [...queue, msg]);
  }

  public async flushOfflineQueue(): Promise<void> {
    const queue = this.offlineQueue();
    if (queue.length === 0 || !this.isConnected() || !this.hubConnection) return;

    const remaining: BufferedMessage[] = [];
    for (const msg of queue) {
      try {
        await this.hubConnection.invoke('BroadcastTyping', msg.conversationId, 'System', false);
        this.offlineMessageFlushed$.next(msg);
      } catch {
        remaining.push(msg);
      }
    }
    this.offlineQueue.set(remaining);
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

  public async lockConversation(conversationId: string, lockedByUserId: string, agentName: string): Promise<void> {
    if (this.hubConnection && this.isConnected()) {
      await this.hubConnection.invoke('LockConversation', conversationId, lockedByUserId, agentName);
    }
  }

  public async unlockConversation(conversationId: string): Promise<void> {
    if (this.hubConnection && this.isConnected()) {
      await this.hubConnection.invoke('UnlockConversation', conversationId);
    }
  }
}
