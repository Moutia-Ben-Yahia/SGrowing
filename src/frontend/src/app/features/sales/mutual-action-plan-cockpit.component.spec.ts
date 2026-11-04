// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { MutualActionPlanCockpitComponent } from './mutual-action-plan-cockpit.component';

describe('MutualActionPlanCockpitComponent', () => {
  it('should initialize with plans and milestones', () => {
    const comp = new MutualActionPlanCockpitComponent();
    expect(comp.plans().length).toBe(2);
    expect(comp.milestones().length).toBe(2);
    expect(comp.milestoneCompletionRatePct()).toBeGreaterThan(50);
  });

  it('should complete milestone and prepend completed milestone to list', () => {
    const comp = new MutualActionPlanCockpitComponent();
    comp.completeMilestone();
    expect(comp.milestones().length).toBe(3);
    expect(comp.milestones()[0].isCompleted).toBe(true);
    expect(comp.milestones()[0].title).toContain('Budget Signoff');
  });

  it('should verify total pipeline value and on-track deal status across active plans', () => {
    const comp = new MutualActionPlanCockpitComponent();
    const totalPipelineUsd = comp.plans().reduce((sum, p) => sum + p.dealValueUsd, 0);
    expect(totalPipelineUsd).toBe(730000); // 450,000 + 280,000
    expect(comp.plans().every(p => p.status === 'ON_TRACK_TO_CLOSE')).toBe(true);
  });

  it('should allow updating milestoneCompletionRatePct and adding custom plans', () => {
    const comp = new MutualActionPlanCockpitComponent();
    comp.milestoneCompletionRatePct.set(88.5);
    comp.plans.update(list => [
      ...list,
      {
        planId: 'MAP-CORP-03',
        accountName: 'Nordic Bank AB',
        opportunityId: 'OPP-BANK-101',
        dealValueUsd: 520000,
        executiveBuyerChampion: 'Lars Lindqvist',
        targetGoLiveDate: '2026-12-31',
        totalMilestonesCount: 6,
        completedMilestonesCount: 4,
        status: 'ON_TRACK_TO_CLOSE'
      }
    ]);

    expect(comp.milestoneCompletionRatePct()).toBe(88.5);
    expect(comp.plans().length).toBe(3);
    expect(comp.plans()[2].planId).toBe('MAP-CORP-03');
  });
});
