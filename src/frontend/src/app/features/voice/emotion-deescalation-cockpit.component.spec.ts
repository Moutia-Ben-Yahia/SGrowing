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
});
