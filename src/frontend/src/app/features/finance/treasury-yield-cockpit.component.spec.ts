// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TreasuryYieldCockpitComponent } from './treasury-yield-cockpit.component';

describe('TreasuryYieldCockpitComponent', () => {
  it('initializes with projections, allocations, and calculates blended APY', () => {
    const comp = new TreasuryYieldCockpitComponent();
    expect(comp.projections().length).toBe(3);
    expect(comp.allocations().length).toBe(3);
    expect(comp.cashReserve()).toBe(7450000);
    expect(comp.surplusCapital()).toBe(4950000);
    expect(comp.blendedApy()).toBeGreaterThan(5.0);
    expect(comp.runwayMonths()).toBeGreaterThan(6.0);
  });

  it('sweeps surplus capital into treasury instrument and updates allocation', () => {
    const comp = new TreasuryYieldCockpitComponent();
    const prevAlloc = comp.allocations().find(a => a.instrumentId === 'US-TBILL-4W')!.currentAllocatedAmount;
    comp.sweep('US-TBILL-4W');

    const updated = comp.allocations().find(a => a.instrumentId === 'US-TBILL-4W');
    expect(updated?.currentAllocatedAmount).toBe(prevAlloc + 250000);
    expect(comp.cashReserve()).toBe(7200000);
    expect(comp.lastSweepNotice()).toContain('Automated Sweep');
  });
});
