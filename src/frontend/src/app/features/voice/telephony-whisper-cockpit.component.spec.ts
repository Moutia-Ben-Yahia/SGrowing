// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TelephonyWhisperCockpitComponent } from './telephony-whisper-cockpit.component';

describe('TelephonyWhisperCockpitComponent', () => {
  it('should initialize with active calls and latency target', () => {
    const comp = new TelephonyWhisperCockpitComponent();
    expect(comp.activeCallsCount()).toBe(2);
    expect(comp.avgLatency()).toBe(73.2);
    expect(comp.sessions().length).toBe(2);
    expect(comp.whispers().length).toBe(2);
  });

  it('should inject whisper prompt and trigger full supervisory takeover', () => {
    const comp = new TelephonyWhisperCockpitComponent();
    const initialWhispers = comp.totalWhispers();
    comp.injectWhisper(comp.sessions()[0]);

    expect(comp.totalWhispers()).toBe(initialWhispers + 1);
    expect(comp.whispers()[0].contextCategory).toBe('PRICING_CONCESSION');

    comp.takeoverCall(comp.sessions()[0]);
    expect(comp.takeoverCount()).toBe(1);
    expect(comp.sessions()[0].activeSupervisionMode).toBe('FULL_TAKEOVER');
  });

  it('should verify clean compliance state and dual-channel prompt acknowledgement tracking', () => {
    const comp = new TelephonyWhisperCockpitComponent();
    expect(comp.sessions().every(s => s.complianceStatus === 'CLEAN')).toBe(true);

    const acknowledgedWhispers = comp.whispers().filter(w => w.agentAcknowledged);
    expect(acknowledgedWhispers.length).toBe(2);

    const categories = comp.whispers().map(w => w.contextCategory);
    expect(categories).toContain('TECH_SPEC');
    expect(categories).toContain('PRICING_CONCESSION');
  });
});
