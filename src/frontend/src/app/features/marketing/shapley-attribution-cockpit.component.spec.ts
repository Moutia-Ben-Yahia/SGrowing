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

  it('handles unknown channel rebalance safely and supports downscaling over-credited channels', () => {
    const comp = new ShapleyAttributionCockpitComponent();
    comp.rebalance('NON-EXISTENT-CHANNEL');
    expect(comp.rebalanceLog()).toBeNull();

    // Rebalance over-credited channel
    comp.rebalance('SocialRetargeting');
    const retargeting = comp.channels().find(c => c.channelId === 'SocialRetargeting');
    expect(retargeting?.currentSpend).toBe(55000); // 85000 - 30000
    expect(retargeting?.recommendedSpendShift).toBe(0);
    expect(comp.rebalanceLog()).toContain('Reallocated $30,000 on LinkedIn & Meta ABM Retargeting');
  });

  it('should return 0 blendedRoas and 0 totalSpend when channels list is empty', () => {
    const comp = new ShapleyAttributionCockpitComponent();
    comp.channels.set([]);
    expect(comp.totalSpend()).toBe(0);
    expect(comp.blendedRoas()).toBe(0);
  });

  it('should recalculate blendedRoas and projectedUplift when totalPipelineArr changes', () => {
    const comp = new ShapleyAttributionCockpitComponent();
    comp.totalPipelineArr.set(2450000);
    expect(comp.blendedRoas()).toBe(10); // 2450000 / 245000
    expect(comp.projectedUplift()).toBe(Math.round(2450000 * 0.185));
  });
});
