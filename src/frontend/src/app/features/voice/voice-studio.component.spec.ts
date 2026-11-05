// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { VoiceStudioComponent } from './voice-studio.component';

describe('VoiceStudioComponent', () => {
  let component: VoiceStudioComponent;

  beforeEach(() => {
    vi.useFakeTimers();
    component = new VoiceStudioComponent();
    component.ngOnInit();
  });

  afterEach(() => {
    component.endCall();
    vi.useRealTimers();
  });

  it('should initialize with default dial number and standard DTMF keypad configuration', () => {
    expect(component).toBeTruthy();
    expect(component.dialNumber).toBe('+1 (555) 303-9999');
    expect(component.isCallActive()).toBe(false);
    expect(component.keypad().length).toBe(12);
    expect(component.callLogs().length).toBeGreaterThan(0);
  });

  it('should append pressed keypad digit to dial number', () => {
    component.dialNumber = '+1';
    component.pressKey('8');
    component.pressKey('0');
    component.pressKey('0');

    expect(component.dialNumber).toBe('+1800');
  });

  it('should start call session, increment active duration seconds, and stop cleanly on endCall', () => {
    component.dialNumber = '+15551234567';
    component.startCall();

    expect(component.isCallActive()).toBe(true);
    expect(component.activeCallSeconds()).toBe(0);

    vi.advanceTimersByTime(3000);
    expect(component.activeCallSeconds()).toBe(3);

    component.endCall();
    expect(component.isCallActive()).toBe(false);

    vi.advanceTimersByTime(2000);
    expect(component.activeCallSeconds()).toBe(3); // Timer was cleared
  });

  it('should initiate inbound simulated VIP call with preconfigured dialer', () => {
    component.simulateInboundCall();

    expect(component.dialNumber).toBe('+1 (555) 303-9999');
    expect(component.isCallActive()).toBe(true);
  });

  it('should allow clearing and adding records to callLogs list', () => {
    component.callLogs.set([]);
    expect(component.callLogs().length).toBe(0);

    component.callLogs.set([
      {
        id: '99',
        caller: '+15550001111',
        contactName: 'Bruce Wayne',
        direction: 'Inbound',
        status: 'Completed',
        durationSeconds: 60,
        timeAgo: 'Just now',
        transcriptSnippet: 'Security architecture audit inquiry.'
      }
    ]);
    expect(component.callLogs().length).toBe(1);
    expect(component.callLogs()[0].contactName).toBe('Bruce Wayne');
  });
});
