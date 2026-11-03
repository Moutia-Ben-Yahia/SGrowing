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
});
