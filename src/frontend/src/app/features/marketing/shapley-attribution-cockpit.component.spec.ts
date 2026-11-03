// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ShapleyAttributionCockpitComponent } from './shapley-attribution-cockpit.component';

describe('ShapleyAttributionCockpitComponent', () => {
  it('initializes with 5 channels and computes blended ROAS', () => {
    const comp = new ShapleyAttributionCockpitComponent();
    expect(comp.channels().length).toBe(5);
    expect(comp.totalSpend()).toBe(245000);
    expect(comp.blendedRoas()).toBeGreaterThan(7.0);
    expect(comp.projectedUplift()).toBeGreaterThan(300000);
  });

  it('reallocates spend based on Shapley recommendations', () => {
    const comp = new ShapleyAttributionCockpitComponent();
    comp.rebalance('Webinars');

    const updated = comp.channels().find(c => c.channelId === 'Webinars');
    expect(updated?.currentSpend).toBe(80000); // 45000 + 35000
    expect(updated?.recommendedSpendShift).toBe(0);
    expect(comp.rebalanceLog()).toContain('Autopilot: Reallocated');
  });
});
