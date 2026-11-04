import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { DealAutopsyCockpitComponent } from './deal-autopsy-cockpit.component';

describe('DealAutopsyCockpitComponent', () => {
  it('should initialize with completed autopsies and recovery playbooks', () => {
    const comp = new DealAutopsyCockpitComponent();
    expect(comp.autopsiesCount()).toBe(48);
    expect(comp.recoveryPotentialPct()).toBe(34.2);
    expect(comp.autopsies().length).toBeGreaterThan(0);
    expect(comp.playbooks().length).toBeGreaterThan(0);
  });

  it('should run a new deal autopsy and update list', () => {
    const comp = new DealAutopsyCockpitComponent();
    const initialCount = comp.autopsies().length;
    comp.analyzeNewLostDeal();
    expect(comp.autopsies().length).toBe(initialCount + 1);
    expect(comp.autopsies()[comp.autopsies().length - 1].accountName).toBe('OmniHealth BioSystems');
  });

  it('should categorize deal loss friction clusters and evaluate pipeline revival probability', () => {
    const comp = new DealAutopsyCockpitComponent();
    expect(comp.autopsies().every(a => a.confidenceScore > 0.90)).toBe(true);

    const firstPlaybook = comp.playbooks()[0];
    expect(firstPlaybook.revivalProbability).toBeGreaterThan(0.5);
    expect(firstPlaybook.cooldownDays).toBe(45);
    expect(firstPlaybook.recoveryStrategy).toContain('Payback Guarantee');
  });

  it('should allow updating lostPipelineUsd and activePlaybooksCount signals', () => {
    const comp = new DealAutopsyCockpitComponent();
    comp.lostPipelineUsd.set(8500000);
    comp.activePlaybooksCount.set(3);

    expect(comp.lostPipelineUsd()).toBe(8500000);
    expect(comp.activePlaybooksCount()).toBe(3);
  });
});
