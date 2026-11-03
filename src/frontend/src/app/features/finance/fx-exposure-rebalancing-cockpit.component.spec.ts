// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { FxExposureRebalancingCockpitComponent } from './fx-exposure-rebalancing-cockpit.component';

describe('FxExposureRebalancingCockpitComponent', () => {
  it('should initialize with correct default metrics and currency positions', () => {
    const comp = new FxExposureRebalancingCockpitComponent();
    expect(comp.totalPortfolioVar()).toBe(589000);
    expect(comp.totalHedgedNotional()).toBe(6000000);
    expect(comp.hedgeRatio()).toBe(84.5);
    expect(comp.maxDrift()).toBe(5.0);
    expect(comp.exposures().length).toBe(4);
  });

  it('should optimize hedge ratio on risk evaluation', () => {
    const comp = new FxExposureRebalancingCockpitComponent();
    comp.evaluateCurrencyRisk();
    expect(comp.hedgeRatio()).toBe(89.2);
  });

  it('should increase hedged notional and reduce currency drift on forward hedge execution', () => {
    const comp = new FxExposureRebalancingCockpitComponent();
    comp.executeForwardHedge();
    expect(comp.totalHedgedNotional()).toBe(7500000);
    const gbp = comp.exposures().find(e => e.currencyCode === 'GBP');
    expect(gbp?.unhedgedDriftPct).toBe(3.1);
  });
});
