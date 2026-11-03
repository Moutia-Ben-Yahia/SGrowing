// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { VoiceIntentCockpitComponent } from './voice-intent-cockpit.component';

describe('VoiceIntentCockpitComponent', () => {
  it('should initialize with fast-path hit rate, P99 latency, and dialect profiles', () => {
    const comp = new VoiceIntentCockpitComponent();
    expect(comp.fastPathHitRate()).toBe(93.6);
    expect(comp.p99Latency()).toBe(18.4);
    expect(comp.totalClassifications()).toBe(12850);
    expect(comp.acousticConfidence()).toBe(0.96);
    expect(comp.predictions().length).toBe(2);
    expect(comp.dialects().length).toBe(2);
  });

  it('should classify new speech utterance and increment total count', () => {
    const comp = new VoiceIntentCockpitComponent();
    const initialClassifications = comp.totalClassifications();
    comp.classifyNewIntent();

    expect(comp.totalClassifications()).toBe(initialClassifications + 1);
    expect(comp.predictions()[2].fastPathRouted).toBe(true);
    expect(comp.predictions()[2].dialectCode).toBe('EN-AU-SYDNEY');
  });
});
