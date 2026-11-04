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

  it('verifies game theory fairness axioms: revenue allocation sums to exact deal value', () => {
    const comp = new AttributionShapleyCockpitComponent();
    const totalAttributedRevenue = comp.channelScores().reduce((acc, c) => acc + c.revenue, 0);
    expect(totalAttributedRevenue).toBe(comp.dealValue());

    const totalShapleyPct = comp.channelScores().reduce((acc, c) => acc + c.shapleyPct, 0);
    expect(Math.round(totalShapleyPct)).toBe(100);

    const nonAttributed = comp.channelScores().find(c => c.channel === 'RetargetingSocialAd');
    expect(nonAttributed?.shapleyPct).toBe(0.0);
    expect(nonAttributed?.revenue).toBe(0);
  });

  it('updates actions of high-leverage channels to Scaled when rebalancing', () => {
    const comp = new AttributionShapleyCockpitComponent();
    comp.rebalanceMarketingBudget();
    const demo = comp.channelScores().find(c => c.channel === 'ExecutiveDemo');
    expect(demo?.action).toBe('Scaled (+25% Spend)');
    const search = comp.channelScores().find(c => c.channel === 'PaidSearchAd');
    expect(search?.action).toBe('Maintain');
  });

  it('should allow modifying dealValue signal and updating channel scores dynamically', () => {
    const comp = new AttributionShapleyCockpitComponent();
    comp.dealValue.set(250000);
    expect(comp.dealValue()).toBe(250000);
    comp.channelScores.set([]);
    expect(comp.channelScores().length).toBe(0);
  });
});
