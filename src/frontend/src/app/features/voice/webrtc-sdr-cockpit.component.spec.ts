// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { WebRtcSdrCockpitComponent } from './webrtc-sdr-cockpit.component';

describe('WebRtcSdrCockpitComponent', () => {
  let component: WebRtcSdrCockpitComponent;

  beforeEach(() => {
    component = new WebRtcSdrCockpitComponent();
  });

  it('should initialize in Idle state', () => {
    expect(component).toBeTruthy();
    expect(component.callStatus()).toBe('Idle');
    expect(component.transcript().length).toBe(0);
  });

  it('should start call and populate first AI turn', () => {
    component.startCall();
    expect(component.callStatus()).toBe('Active');
    expect(component.vadState()).toBe('AgentSpeaking');
    expect(component.transcript().length).toBe(1);
    expect(component.transcript()[0].speaker).toBe('AI_SDR');
  });

  it('should simulate barge-in and advance turn count', () => {
    component.startCall();
    component.simulateLeadBargeIn();
    expect(component.vadState()).toBe('BargeInInterrupted');
    expect(component.turnCount()).toBeGreaterThan(1);
    expect(component.transcript().some(t => t.speaker === 'Lead')).toBe(true);
  });

  it('should end call', () => {
    component.startCall();
    component.endCall();
    expect(component.callStatus()).toBe('Completed');
    expect(component.vadState()).toBe('Silence');
  });
});
