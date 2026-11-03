// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { Subject, of } from 'rxjs';
import { SoftphoneDialerComponent } from './softphone-dialer.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SignalRService } from '../../core/services/signalr.service';
import { InboxSlotAllocationDto } from '../../core/models/inspiration.models';

describe('SoftphoneDialerComponent', () => {
  let component: SoftphoneDialerComponent;
  let apiMock: any;
  let authMock: any;
  let signalRMock: any;
  let voiceTranscriptionChunkSubject: Subject<any>;

  const mockSlots: InboxSlotAllocationDto[] = [
    {
      senderAccountId: 'acc-1',
      senderEmail: 'sdr1@outbound.io',
      sendingDomain: 'outbound.io',
      allocatedSlotsRemaining: 24,
      dailyCap: 50,
      pacingIntervalSeconds: 45
    }
  ];

  beforeEach(() => {
    voiceTranscriptionChunkSubject = new Subject();

    apiMock = {
      allocateInboxSlots: vi.fn().mockReturnValue(of(mockSlots)),
      initiateCallSession: vi.fn().mockReturnValue(of({ callSessionId: 'sess-123', status: 'Ringing' })),
      endCallSession: vi.fn().mockReturnValue(of({ success: true })),
      classifyInboundReply: vi.fn().mockReturnValue(of({
        category: 1,
        confidenceScore: 0.94,
        recommendedAction: 'Book Demo on Calendar',
        autoCalendarLinkInjected: true
      })),
      streamVoiceConversationTurn: vi.fn().mockReturnValue(of({
        agentResponseText: 'Our enterprise tier starts at $999/mo.',
        detectedBuyingSignals: ['PricingInquiry'],
        sentimentScore: 0.75
      })),
      submitCallDisposition: vi.fn().mockReturnValue(of({ success: true }))
    };

    authMock = {
      currentTenant: vi.fn().mockReturnValue({ id: 'tenant-123', name: 'Test Tenant' })
    };

    signalRMock = {
      voiceTranscriptionChunk$: voiceTranscriptionChunkSubject.asObservable()
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiMock },
      { provide: AuthService, useValue: authMock },
      { provide: SignalRService, useValue: signalRMock }
    ]);

    injector.runInContext(() => {
      component = new SoftphoneDialerComponent();
      component.ngOnInit();
    });
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('initializes and allocates inbox slots', () => {
    expect(apiMock.allocateInboxSlots).toHaveBeenCalled();
    expect(component.inboxSlots().length).toBe(1);
    expect(component.dialButtons.length).toBe(12);
  });

  it('appends key presses to dialed number', () => {
    component.dialedNumber = '+1';
    component.pressKey('5');
    component.pressKey('5');
    component.pressKey('5');

    expect(component.dialedNumber).toBe('+1555');
  });

  it('computes sentiment percentages and descriptive labels accurately', () => {
    component.sentimentScore.set(0.7);
    expect(component.getSentimentLabel()).toBe('High Purchase Intent');
    expect(component.getSentimentColorClass()).toBe('text-emerald-400');
    expect(component.sentimentPercent()).toBeGreaterThan(70);

    component.sentimentScore.set(-0.4);
    expect(component.getSentimentLabel()).toBe('Hesitant / Objection');
    expect(component.getSentimentColorClass()).toBe('text-rose-400');
  });

  it('maps buying signal classes and icons', () => {
    expect(component.getBuyingSignalClass('PricingInquiry')).toContain('text-cyan-300');
    expect(component.getBuyingSignalIcon('PricingInquiry')).toBe('💰');

    expect(component.getBuyingSignalClass('ContractRequest')).toContain('text-emerald-300');
    expect(component.getBuyingSignalIcon('ContractRequest')).toBe('📜');

    expect(component.getBuyingSignalIcon('DemoBooking')).toBe('📅');
  });

  it('processes incoming SignalR transcription chunks reactively', () => {
    voiceTranscriptionChunkSubject.next({
      speaker: 'Caller',
      chunkText: 'How soon can we deploy this?',
      sentimentScore: 0.82,
      isFinal: true
    });

    expect(component.transcriptionTurns().length).toBe(1);
    expect(component.transcriptionTurns()[0].text).toBe('How soon can we deploy this?');
    expect(component.sentimentScore()).toBe(0.82);
    expect(component.vadStatus()).toBe('Listening');
  });
});
