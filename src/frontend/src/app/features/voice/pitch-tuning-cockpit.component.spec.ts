// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { PitchTuningCockpitComponent } from './pitch-tuning-cockpit.component';

describe('PitchTuningCockpitComponent', () => {
  it('initializes with active calls and average sentiment', () => {
    const comp = new PitchTuningCockpitComponent();
    expect(comp.activeCalls().length).toBe(3);
    expect(comp.averageSentimentLabel()).toBe('Curious / Positive');
  });

  it('tunes pitch DSP and modulates speech rate and F0', () => {
    const comp = new PitchTuningCockpitComponent();
    const originalF0 = comp.activeCalls()[0].fundamentalFrequencyHz;
    comp.tune('CALL-VOICE-9801');

    const tuned = comp.activeCalls().find(c => c.callId === 'CALL-VOICE-9801');
    expect(tuned?.fundamentalFrequencyHz).toBeLessThan(originalF0);
    expect(tuned?.speechRateWpm).toBe(130);
    expect(tuned?.currentPersona).toBe('Empathic');
    expect(comp.lastDspNotice()).toContain('DSP Applied');
  });

  it('simulates in-flight caller sentiment shift and updates pivot angle', () => {
    const comp = new PitchTuningCockpitComponent();
    comp.simulateShift('CALL-VOICE-9801');

    const shifted = comp.activeCalls().find(c => c.callId === 'CALL-VOICE-9801');
    expect(shifted?.currentSentiment).toBe('Enthusiastic');
    expect(shifted?.currentPersona).toBe('Assertive');
    expect(shifted?.speechRateWpm).toBe(160);
    expect(comp.lastDspNotice()).toContain('Transitioned sentiment to Enthusiastic');
  });
});
