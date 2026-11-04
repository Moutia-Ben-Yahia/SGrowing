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

  it('should identify skeptical procurement blockers and generate targeted ROI sessions', () => {
    const comp = new BuyingCommitteeCockpitComponent();
    const blocker = comp.stakeholders().find(s => s.buyingRole === 'PROCUREMENT_BLOCKER');
    expect(blocker).toBeDefined();
    expect(blocker?.sentimentScore).toBeLessThan(0);
    expect(blocker?.department).toBe('Global Sourcing');

    comp.triggerIntervention(blocker!);
    const lastInt = comp.interventions()[comp.interventions().length - 1];
    expect(lastInt.expectedOutcome).toContain('Alicia Thorne');
    expect(lastInt.playbookType).toBe('ROI_CALCULATOR_SESSION');
  });

  it('should allow updating consensusScore, alignedChampions, and activeBlockers signals', () => {
    const comp = new BuyingCommitteeCockpitComponent();
    comp.consensusScore.set(91.2);
    comp.alignedChampions.set(4);
    comp.activeBlockers.set(0);

    expect(comp.consensusScore()).toBe(91.2);
    expect(comp.alignedChampions()).toBe(4);
    expect(comp.activeBlockers()).toBe(0);
  });
});
