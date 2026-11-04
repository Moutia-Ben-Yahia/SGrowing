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

  it('should verify sub-20ms inference latency and regional dialect acoustic normalization feature counts', () => {
    const comp = new VoiceIntentCockpitComponent();
    expect(comp.predictions().every(p => p.inferenceLatencyMs < 20.0)).toBe(true);
    expect(comp.dialects().every(d => d.acousticFeatureCount === 128)).toBe(true);

    const texasDialect = comp.dialects().find(d => d.dialectCode === 'EN-TX-AUSTIN');
    expect(texasDialect?.fastPathThresholdCutoff).toBe(0.85);

    const glasgowDialect = comp.dialects().find(d => d.dialectCode === 'EN-UK-GLASGOW');
    expect(glasgowDialect?.fastPathThresholdCutoff).toBe(0.88);
  });

  it('should allow updating fastPathHitRate and p99Latency signals', () => {
    const comp = new VoiceIntentCockpitComponent();
    comp.fastPathHitRate.set(96.2);
    comp.p99Latency.set(16.5);
    comp.acousticConfidence.set(0.985);

    expect(comp.fastPathHitRate()).toBe(96.2);
    expect(comp.p99Latency()).toBe(16.5);
    expect(comp.acousticConfidence()).toBe(0.985);
  });
});
