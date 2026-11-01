// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { VoiceStreamingCockpitComponent } from './voice-streaming-cockpit.component';

describe('VoiceStreamingCockpitComponent (Low-Latency Voice AI & Barge-In Cockpit)', () => {
  let component: VoiceStreamingCockpitComponent;

  const mockProfiles = [
    {
      id: 'sophia-enterprise',
      name: 'Sophia (Enterprise AE)',
      voiceModel: 'azure-neural-sophia',
      persona: 'Executive B2B AE',
      baseLatencyMs: 145,
      speakingRate: 1.05,
      languageCode: 'en-US',
      bargeInEnabled: true
    }
  ];

  const mockSessions = [
    {
      sessionId: '00000000-0000-0000-0000-000000000050',
      tenantId: '00000000-0000-0000-0000-000000000001',
      callSid: 'CA_STREAM_ENTERPRISE_50',
      lifecycle: 'ActiveAudio',
      pipelineState: 'AgentSpeaking',
      currentInputDb: -45,
      currentOutputDb: -12,
      averageTurnaroundLatencyMs: 412,
      minTurnaroundLatencyMs: 380,
      maxTurnaroundLatencyMs: 485,
      callerNumber: '+1 (415) 890-2341',
      calleeNumber: '+1 (800) 555-0199',
      transcript: [],
      recentTurnLatencies: [
        {
          turnId: 'turn-1',
          userSpeechEndedUtc: '2026-11-01T10:00:00Z',
          vadSilenceDetectedUtc: '2026-11-01T10:00:00.080Z',
          transcriptionCompletedUtc: '2026-11-01T10:00:00.180Z',
          llmThoughtGeneratedUtc: '2026-11-01T10:00:00.320Z',
          ttsFirstChunkEmittedUtc: '2026-11-01T10:00:00.412Z',
          vadLatencyMs: 80,
          asrLatencyMs: 100,
          llmLatencyMs: 140,
          ttsLatencyMs: 92,
          totalTurnaroundMs: 412,
          isSub600Ms: true,
          latencyGrade: 'Sub500ms'
        }
      ]
    }
  ];

  const mockHttp = {
    get: (url: string) => {
      if (url.includes('profiles')) return of(mockProfiles);
      if (url.includes('sessions')) return of(mockSessions);
      return of({});
    },
    post: () => of({ success: true })
  };

  beforeEach(() => {
    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new VoiceStreamingCockpitComponent());
    component.ngOnInit();
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('should initialize with voice profiles and active session', () => {
    expect(component.profiles().length).toBeGreaterThanOrEqual(1);
    expect(component.sessions().length).toBeGreaterThanOrEqual(1);
    expect(component.selectedSessionId()).toBe('00000000-0000-0000-0000-000000000050');
    expect(component.activeSession()).not.toBeNull();
    expect(component.activeSession()?.pipelineState).toBe('AgentSpeaking');
  });

  it('should compute dB meter percentage correctly', () => {
    expect(component.calcDbPercentage(-90)).toBe(0);
    expect(component.calcDbPercentage(0)).toBe(100);
    expect(component.calcDbPercentage(-45)).toBe(50);
  });

  it('should compute latestTurnaround metric from active session', () => {
    const turnaround = component.latestTurnaround();
    expect(turnaround).not.toBeNull();
    expect(turnaround?.totalTurnaroundMs).toBe(412);
    expect(turnaround?.isSub600Ms).toBe(true);
    expect(turnaround?.vadLatencyMs).toBe(80);
    expect(turnaround?.asrLatencyMs).toBe(100);
  });

  it('should toggle mute signal correctly', () => {
    expect(component.isMuted()).toBe(false);
    component.toggleMute();
    expect(component.isMuted()).toBe(true);
    component.toggleMute();
    expect(component.isMuted()).toBe(false);
  });

  it('should return appropriate pipeline state badge and dot classes', () => {
    expect(component.getPipelineStateBadgeClass('Listening')).toContain('bg-blue-950');
    expect(component.getPipelineStateBadgeClass('AgentSpeaking')).toContain('bg-emerald-950');
    expect(component.getPipelineStateBadgeClass('BargeInFlush')).toContain('bg-amber-950');

    expect(component.getPipelineStateDotClass('Listening')).toContain('bg-blue-400');
    expect(component.getPipelineStateDotClass('AgentSpeaking')).toContain('bg-emerald-400');
  });

  it('should generate waveform oscilloscope bars', () => {
    const bars = component.waveformBars();
    expect(bars.length).toBe(36);
    expect(bars[0].height).toBeGreaterThan(0);
  });
});
