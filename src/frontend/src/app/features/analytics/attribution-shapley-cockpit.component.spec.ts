// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { AttributionShapleyCockpitComponent } from './attribution-shapley-cockpit.component';

describe('AttributionShapleyCockpitComponent', () => {
  it('initializes with deal value and Shapley fair value allocations', () => {
    const comp = new AttributionShapleyCockpitComponent();
    expect(comp.dealValue()).toBe(150000);
    expect(comp.channelScores().length).toBe(5);
    expect(comp.channelScores().find(c => c.channel === 'ExecutiveDemo')?.shapleyPct).toBe(38.5);
  });

  it('rebalances marketing budget and updates advisory actions', () => {
    const comp = new AttributionShapleyCockpitComponent();
    expect(comp.budgetRebalanced()).toBe(false);
    comp.rebalanceMarketingBudget();
    expect(comp.budgetRebalanced()).toBe(true);
    expect(comp.channelScores().some(c => c.action.includes('Trimmed'))).toBe(true);
  });
});
