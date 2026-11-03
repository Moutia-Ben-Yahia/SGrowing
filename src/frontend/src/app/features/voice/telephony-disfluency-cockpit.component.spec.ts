// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TelephonyDisfluencyCockpitComponent } from './telephony-disfluency-cockpit.component';

describe('TelephonyDisfluencyCockpitComponent', () => {
  it('should initialize with analyzed calls, silence duration, and disfluency events', () => {
    const comp = new TelephonyDisfluencyCockpitComponent();
    expect(comp.analyzedSessionsCount()).toBe(142);
    expect(comp.avgSilenceDurationMs()).toBe(1035.0);
    expect(comp.disfluenciesCount()).toBe(87);
    expect(comp.turnTakingIndex()).toBe(94.6);
    expect(comp.events().length).toBe(2);
    expect(comp.profiles().length).toBe(2);
  });

  it('should analyze new call cadence event and increment disfluency count', () => {
    const comp = new TelephonyDisfluencyCockpitComponent();
    const initialDisfluencies = comp.disfluenciesCount();
    comp.analyzeNewCallCadence();

    expect(comp.disfluenciesCount()).toBe(initialDisfluencies + 1);
    expect(comp.events()[2].disfluencyType).toBe('PROLONGATION');
    expect(comp.events()[2].callSessionId).toBe('CALL-DISF-903');
  });
});
