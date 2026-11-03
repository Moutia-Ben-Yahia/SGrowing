// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { InboxShellComponent } from './inbox-shell.component';
import { SignalRService } from '../../core/services/signalr.service';
import { AuthService } from '../../core/services/auth.service';
import { TranslationService } from '../../core/services/translation.service';
import { ApiService } from '../../core/services/api.service';
import { HttpClient } from '@angular/common/http';
import { MessageDto, AiDraftDto } from '../../core/models/chat.models';

describe('InboxShellComponent (Omnichannel Inbox Shell)', () => {
  let component: InboxShellComponent;
  let mockSignalR: any;
  let mockAuth: any;
  let mockTranslation: any;
  let mockApi: any;

  beforeEach(() => {
    localStorage.clear();

    mockSignalR = {
      isConnected: signal(true),
      connectionState: signal('Connected'),
      offlineQueue: signal([]),
      messageReceived$: new Subject<MessageDto>(),
      conversationUpdated$: new Subject<any>(),
      aiDraftGenerated$: new Subject<AiDraftDto>(),
      typingIndicator$: new Subject<any>(),
      conversationLocked$: new Subject<any>(),
      conversationUnlocked$: new Subject<any>(),
      offlineMessageFlushed$: new Subject<any>(),
      startConnection: vi.fn().mockResolvedValue(undefined),
      enqueueOfflineMessage: vi.fn()
    };

    mockAuth = {
      currentUser: signal({
        id: 'u1',
        fullName: 'Admin Agent',
        email: 'agent@sgrowing.com',
        roles: ['Admin']
      })
    };

    mockTranslation = {
      t: (key: string) => key,
      currentLocale: signal('en-US'),
      setLocale: vi.fn()
    };

    mockApi = {
      get: vi.fn(),
      post: vi.fn()
    };

    const injector = Injector.create({
      providers: [
        { provide: SignalRService, useValue: mockSignalR },
        { provide: AuthService, useValue: mockAuth },
        { provide: TranslationService, useValue: mockTranslation },
        { provide: ApiService, useValue: mockApi },
        { provide: HttpClient, useValue: {} }
      ]
    });

    component = runInInjectionContext(injector, () => new InboxShellComponent());
    component.ngOnInit();
  });

  it('should initialize and connect to SignalR hub and seed demo conversations', () => {
    expect(mockSignalR.startConnection).toHaveBeenCalled();
    expect(component.conversations().length).toBeGreaterThan(0);
    expect(component.activeConversation()).toBeDefined();
    expect(component.activeMessages().length).toBeGreaterThan(0);
  });

  it('should select active conversation and load associated messages and draft', () => {
    const conv = component.conversations()[0];
    component.selectConversation(conv);

    expect(component.activeConversation()?.id).toBe(conv.id);
    expect(component.activeMessages().length).toBe(2);
    expect(component.currentAiDraft()).not.toBeNull();
  });

  it('should send a message in online mode and append to active messages', () => {
    const initialCount = component.activeMessages().length;
    component.composerText = 'Thank you for contacting SGrowing support!';
    component.sendMessage();

    expect(component.activeMessages().length).toBe(initialCount + 1);
    expect(component.composerText).toBe('');
    const sent = component.activeMessages()[component.activeMessages().length - 1];
    expect(sent.content).toBe('Thank you for contacting SGrowing support!');
    expect(sent.senderType).toBe(2);
  });

  it('should insert quick meeting and checkout links into composer', () => {
    component.insertBookingLink();
    expect(component.composerText).toContain('https://sgrowing.app/book/');

    component.insertCheckoutLink();
    expect(component.composerText).toContain('https://sgrowing.app/pay/');
  });

  it('should approve and apply suggested AI copilot draft', () => {
    expect(component.currentAiDraft()).not.toBeNull();
    const draftText = component.currentAiDraft()!.suggestedReply;
    const initialMsgCount = component.activeMessages().length;

    component.applyAiDraft();

    expect(component.currentAiDraft()).toBeNull();
    expect(component.activeMessages().length).toBe(initialMsgCount + 1);
    const lastMsg = component.activeMessages()[component.activeMessages().length - 1];
    expect(lastMsg.content).toBe(draftText);
  });

  it('should handle real-time inbound message over SignalR subscription', () => {
    const initialCount = component.activeMessages().length;
    const activeConvId = component.activeConversation()!.id;

    mockSignalR.messageReceived$.next({
      id: 'rt-msg-1',
      conversationId: activeConvId,
      senderType: 1, // Contact
      contentType: 1,
      content: 'Real-time customer reply via WhatsApp',
      deliveryStatus: 3,
      createdAtUtc: new Date().toISOString()
    });

    expect(component.activeMessages().length).toBe(initialCount + 1);
    expect(component.activeMessages()[component.activeMessages().length - 1].content).toBe('Real-time customer reply via WhatsApp');
  });
});
