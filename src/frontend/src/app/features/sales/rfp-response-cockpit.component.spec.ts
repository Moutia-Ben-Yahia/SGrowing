// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { RfpResponseCockpitComponent } from './rfp-response-cockpit.component';

describe('RfpResponseCockpitComponent', () => {
  it('should initialize with questionnaires and answers', () => {
    const comp = new RfpResponseCockpitComponent();
    expect(comp.questionnaires().length).toBe(2);
    expect(comp.answers().length).toBe(1);
    expect(comp.questionnaires()[0].confidenceScoreAvgPct).toBeGreaterThan(90);
  });

  it('should generate new answer and prepend to list', () => {
    const comp = new RfpResponseCockpitComponent();
    comp.generateNewAnswer();
    expect(comp.answers().length).toBe(2);
    expect(comp.answers()[0].questionPrompt).toContain('failover');
    expect(comp.answers()[0].synthesizedAnswer).toContain('multi-cloud');
  });
});
