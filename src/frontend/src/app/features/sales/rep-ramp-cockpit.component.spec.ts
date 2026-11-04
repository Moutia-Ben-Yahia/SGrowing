import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { RepRampCockpitComponent } from './rep-ramp-cockpit.component';

describe('RepRampCockpitComponent', () => {
  it('should initialize with active ramping reps and competencies', () => {
    const comp = new RepRampCockpitComponent();
    expect(comp.reps().length).toBeGreaterThan(0);
    expect(comp.selectedRep()).not.toBeNull();
    expect(comp.selectedRep()?.competencies.length).toBeGreaterThan(0);
  });

  it('should generate whisper guidance for pricing objections', () => {
    const comp = new RepRampCockpitComponent();
    comp.liveTranscript = 'Your price is simply too high and over budget.';
    comp.generateGuidance();

    expect(comp.activeGuidance()).not.toBeNull();
    expect(comp.activeGuidance()?.detectedIntent).toContain('Budget');
    expect(comp.activeGuidance()?.recommendedWhisperPrompt).toContain('Net Value');
    expect(comp.activeGuidance()?.confidenceScore).toBe(94);
  });

  it('should generate discovery guidance for general inquiries without pricing objections', () => {
    const comp = new RepRampCockpitComponent();
    comp.selectRep(comp.reps()[1]);
    expect(comp.selectedRep()?.name).toBe('Elena Rostova');

    comp.liveTranscript = 'Can you walk through how the architecture handles peak telemetry loads?';
    comp.generateGuidance();

    expect(comp.activeGuidance()).not.toBeNull();
    expect(comp.activeGuidance()?.repId).toBe('REP-RAMP-102');
    expect(comp.activeGuidance()?.detectedIntent).toBe('Discovery Inquiry');
    expect(comp.activeGuidance()?.confidenceScore).toBe(88);
    expect(comp.activeGuidance()?.microCoachingTip).toContain('executive business impact');
  });

  it('should allow updating rep metrics and competencies', () => {
    const comp = new RepRampCockpitComponent();
    comp.reps.update(list => list.map(r => r.repId === 'REP-RAMP-101' ? { ...r, dealsClosed: 5, quotaAttainmentPercentage: 65.0 } : r));

    const updated = comp.reps().find(r => r.repId === 'REP-RAMP-101');
    expect(updated?.dealsClosed).toBe(5);
    expect(updated?.quotaAttainmentPercentage).toBe(65.0);
  });

  it('should fallback to default repId when selectedRep is null during generateGuidance', () => {
    const comp = new RepRampCockpitComponent();
    comp.selectedRep.set(null);
    comp.liveTranscript = 'Hello discovery message';
    comp.generateGuidance();

    expect(comp.activeGuidance()?.repId).toBe('REP-RAMP-101');
    expect(comp.activeGuidance()?.detectedIntent).toBe('Discovery Inquiry');
  });
});
