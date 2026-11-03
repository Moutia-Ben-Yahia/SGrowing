import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { VocalTremorCockpitComponent } from './vocal-tremor-cockpit.component';

describe('VocalTremorCockpitComponent', () => {
  it('should initialize with audited voice calls and duress classifications', () => {
    const comp = new VocalTremorCockpitComponent();
    expect(comp.sessions().length).toBeGreaterThan(0);
    expect(comp.selectedSession()).not.toBeNull();
  });

  it('should analyze acoustic tremor and trigger supervisor alert for high stress', () => {
    const comp = new VocalTremorCockpitComponent();
    comp.tremorModulationHz = 10.5; // in 8-14 Hz physiological micro-tremor range
    comp.energyDbfs = -45.0; // whisper energy
    comp.analyzeAcoustics();

    expect(comp.analysisResult()).not.toBeNull();
    expect(comp.analysisResult()?.stressScore).toBeGreaterThanOrEqual(75);
    expect(comp.analysisResult()?.alertTriggered).toBe(true);
    expect(comp.analysisResult()?.duressLevel).toBe('SevereDuress');
    expect(comp.analysisResult()?.whisperBoostActive).toBe(true);
  });
});
