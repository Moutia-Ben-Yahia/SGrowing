// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { MutualActionPlanCockpitComponent } from './mutual-action-plan-cockpit.component';

describe('MutualActionPlanCockpitComponent', () => {
  it('initializes with seed plan, champion score, and progress', () => {
    const comp = new MutualActionPlanCockpitComponent();
    expect(comp.plan().milestones.length).toBe(4);
    expect(comp.completedCount()).toBe(1);
    expect(comp.completionPercentage()).toBe(25);
    expect(comp.plan().championHealthScore).toBe(88);
    expect(comp.plan().isAtRiskOfSlipping).toBe(false);
  });

  it('marks a milestone as blocked and triggers slippage risk alert', () => {
    const comp = new MutualActionPlanCockpitComponent();
    const ms2 = comp.plan().milestones.find(m => m.milestoneId === 'ms_02')!;
    comp.markMilestoneBlocked(ms2);

    expect(comp.plan().isAtRiskOfSlipping).toBe(true);
    expect(comp.plan().championHealthScore).toBeLessThan(80);
    expect(comp.plan().slippageRiskRationale).toContain('blocked');
  });

  it('triggers slippage rescue playbook and restores healthy status', () => {
    const comp = new MutualActionPlanCockpitComponent();
    const ms2 = comp.plan().milestones.find(m => m.milestoneId === 'ms_02')!;
    comp.markMilestoneBlocked(ms2);
    expect(comp.plan().isAtRiskOfSlipping).toBe(true);

    comp.triggerSlippageRescue();
    expect(comp.plan().isAtRiskOfSlipping).toBe(false);
    expect(comp.plan().championHealthScore).toBe(85);
  });

  it('verifies all milestones complete and maximizes champion score to 100', () => {
    const comp = new MutualActionPlanCockpitComponent();
    for (const m of comp.plan().milestones) {
      comp.markMilestoneComplete(m);
    }

    expect(comp.completedCount()).toBe(4);
    expect(comp.completionPercentage()).toBe(100);
    expect(comp.plan().championHealthScore).toBe(100);
  });
});
