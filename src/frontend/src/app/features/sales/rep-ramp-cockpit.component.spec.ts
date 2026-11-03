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
});
