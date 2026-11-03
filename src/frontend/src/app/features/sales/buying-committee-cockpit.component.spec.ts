// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { BuyingCommitteeCockpitComponent } from './buying-committee-cockpit.component';

describe('BuyingCommitteeCockpitComponent', () => {
  it('should initialize with consensus score and stakeholders', () => {
    const comp = new BuyingCommitteeCockpitComponent();
    expect(comp.consensusScore()).toBe(74.5);
    expect(comp.totalStakeholders()).toBe(4);
    expect(comp.stakeholders().length).toBe(4);
    expect(comp.interventions().length).toBe(1);
  });

  it('should trigger blocker intervention and append to list', () => {
    const comp = new BuyingCommitteeCockpitComponent();
    const initialCount = comp.interventions().length;
    comp.triggerIntervention(comp.stakeholders()[0]);

    expect(comp.interventions().length).toBe(initialCount + 1);
    expect(comp.interventions()[initialCount].targetStakeholderId).toBe('STK-001');
    expect(comp.interventions()[initialCount].status).toBe('SCHEDULED');
  });
});
