// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { VoiceTranscriptionStudioComponent, DiarizedVoiceCall, PostCallStructuredSummary } from './voice-transcription-studio.component';

describe('VoiceTranscriptionStudioComponent', () => {
  let component: VoiceTranscriptionStudioComponent;
  let mockHttp: any;

  const mockSummary: PostCallStructuredSummary = {
    callId: 'CALL-5120-OUT',
    generatedAt: '2026-11-03T10:00:00Z',
    keyDiscussionPoints: ['Discussed API latency SLA', 'Agreed on 99.9% uptime credit'],
    objectionsRaised: ['High initial setup fee'],
    customerSentimentTrajectory: 'Improving',
    overallSentimentScore: 0.65,
    agreedActionItems: ['Send updated SLA contract addendum'],
    recommendedNextStep: 'Schedule implementation sync',
    crmDealStage: 'Negotiation',
    associatedDealId: 'DEAL-9921',
    executiveBrief: 'Client was initially dissatisfied with latency but accepted our proposed failover routing architecture.'
  };

  const mockCalls: DiarizedVoiceCall[] = [
    {
      callId: 'CALL-5120-OUT',
      tenantId: 'tenant-123',
      callerName: 'Amine Trabelsi',
      callerPhoneNumber: '+216 98 456 789',
      agentName: 'Marcus Vance',
      callTopic: 'Enterprise API SLA & Latency Dispute',
      startedAtUtc: '2026-11-03T09:45:00Z',
      totalDurationSeconds: 312,
      status: 'Live',
      overallSentimentScore: -0.25,
      sentimentTrajectory: 'Deteriorating',
      frustrationAlert: {
        isActive: true,
        severity: 'High',
        triggerReason: 'Customer threatened cancellation',
        triggerKeywords: ['cancel', 'refund', 'unacceptable'],
        empathyGuidanceTalkingPoints: ['Acknowledge latency impact immediately']
      },
      utterances: [
        {
          id: 'u-1',
          callId: 'CALL-5120-OUT',
          speaker: 'Caller',
          text: 'This latency is completely unacceptable! I want to cancel our enterprise account.',
          startOffsetMs: 1200,
          endOffsetMs: 4800,
          durationMs: 3600,
          confidenceScore: 0.98,
          sentimentScore: -0.85,
          sentimentVelocity: -0.3,
          timestamp: '2026-11-03T09:45:05Z',
          words: []
        }
      ]
    }
  ];

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/calls')) return of(mockCalls);
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/generate-summary')) return of(mockSummary);
        if (url.includes('/stream-utterance')) return of({ success: true });
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new VoiceTranscriptionStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load active diarized voice calls', () => {
    expect(component).toBeTruthy();
    expect(mockHttp.get).toHaveBeenCalledWith('/api/v1/voice/diarization/calls');
    expect(component.calls().length).toBe(1);
    expect(component.selectedCallId()).toBe('CALL-5120-OUT');
    expect(component.currentCall()?.callerName).toBe('Amine Trabelsi');
  });

  it('should apply pre-configured simulator speech presets', () => {
    component.applyPreset('cancel');
    expect(component.simulatedSpeaker()).toBe('Caller');
    expect(component.simulatedText()).toContain('completely unacceptable');
    expect(component.simulatedConfidence()).toBe(0.97);

    component.applyPreset('happy');
    expect(component.simulatedText()).toContain('delighted to renew');
  });

  it('should stream simulated utterance via HTTP', () => {
    component.simulatedText.set('We are testing speech stream.');
    component.sendSimulatedUtterance();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/voice/diarization/stream-utterance',
      expect.objectContaining({
        callId: 'CALL-5120-OUT',
        text: 'We are testing speech stream.'
      })
    );
  });

  it('should trigger post-call summary generation and update call state', () => {
    component.generateSummary('CALL-5120-OUT');

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/voice/diarization/generate-summary',
      { callId: 'CALL-5120-OUT' }
    );
    expect(component.currentCall()?.summary?.crmDealStage).toBe('Negotiation');
    expect(component.isGeneratingSummary()).toBe(false);
  });

  it('should allow toggling simulatedSpeaker and updating simulatedConfidence and selectedCallId', () => {
    component.simulatedSpeaker.set('Agent');
    expect(component.simulatedSpeaker()).toBe('Agent');

    component.simulatedConfidence.set(0.99);
    expect(component.simulatedConfidence()).toBe(0.99);

    component.selectedCallId.set('CALL-NON-EXISTENT');
    expect(component.currentCall()).toBeNull();
  });
});
