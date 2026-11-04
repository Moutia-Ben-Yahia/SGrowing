// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { EmotionDeescalationCockpitComponent } from './emotion-deescalation-cockpit.component';

describe('EmotionDeescalationCockpitComponent', () => {
  it('should initialize with calls and escalations', () => {
    const comp = new EmotionDeescalationCockpitComponent();
    expect(comp.calls().length).toBe(2);
    expect(comp.escalations().length).toBe(1);
    expect(comp.deescalationRatePct()).toBeGreaterThan(80);
  });

  it('should trigger warm handoff and add an escalation ticket', () => {
    const comp = new EmotionDeescalationCockpitComponent();
    comp.triggerWarmHandoff();
    expect(comp.escalations().length).toBe(2);
    expect(comp.escalations()[0].status).toBe('SUPERVISOR_ACCEPTED');
  });

  it('should categorize live acoustic telemetry and high-stress frustration thresholds', () => {
    const comp = new EmotionDeescalationCockpitComponent();
    const calmCall = comp.calls().find(c => c.detectedEmotion === 'CALM_COLLABORATIVE');
    expect(calmCall).toBeDefined();
    expect(calmCall?.frustrationScore).toBeLessThan(0.3);
    expect(calmCall?.speechVelocityWordsPerMin).toBeGreaterThan(120);

    const initialEscalation = comp.escalations()[0];
    expect(initialEscalation.peakFrustrationScore).toBeGreaterThan(0.9);
    expect(initialEscalation.suggestedConcessionOffer).toContain('SLA credit');
  });
});
