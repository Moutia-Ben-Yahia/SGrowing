// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TerritoryRebalancingCockpitComponent } from './territory-rebalancing-cockpit.component';

describe('TerritoryRebalancingCockpitComponent', () => {
  it('should initialize with correct default metrics', () => {
    const comp = new TerritoryRebalancingCockpitComponent();
    expect(comp.totalAccounts()).toBe(156);
    expect(comp.giniIndex()).toBe(0.38);
    expect(comp.territories().length).toBe(4);
    expect(comp.recommendations().length).toBe(3);
  });

  it('should update Gini index on simulation and schedule plan', () => {
    const comp = new TerritoryRebalancingCockpitComponent();
    comp.simulateEquilibrium();
    expect(comp.giniIndex()).toBe(0.14);
    expect(comp.planStatus()).toContain('Simulation Active');

    comp.applyRebalance();
    expect(comp.planStatus()).toBe('Scheduled (14-Day Grace)');
  });
});
