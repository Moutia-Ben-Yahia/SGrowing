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

  it('should verify territory workload imbalance index and account distribution', () => {
    const comp = new TerritoryRebalancingCockpitComponent();
    const emea = comp.territories().find(t => t.territoryId === 'TERR-EMEA-CENTRAL');
    expect(emea).toBeDefined();
    expect(emea?.workloadIndex).toBeGreaterThan(1.5);
    expect(emea?.assignedRepId).toBe('REP-103');

    const totalPipeline = comp.territories().reduce((sum, t) => sum + t.totalPipelinePotential, 0);
    expect(totalPipeline).toBe(14700000); // 4.85M + 2.9M + 5.1M + 1.85M
    expect(comp.efficiencyGainPct()).toBe(24.6);
  });

  it('should allow updating totalAccounts and efficiencyGainPct signals', () => {
    const comp = new TerritoryRebalancingCockpitComponent();
    comp.totalAccounts.set(180);
    comp.efficiencyGainPct.set(29.8);

    expect(comp.totalAccounts()).toBe(180);
    expect(comp.efficiencyGainPct()).toBe(29.8);
  });

  it('should support resetting territories and recommendations lists to empty', () => {
    const comp = new TerritoryRebalancingCockpitComponent();
    comp.territories.set([]);
    comp.recommendations.set([]);
    comp.totalAccounts.set(0);

    expect(comp.territories().length).toBe(0);
    expect(comp.recommendations().length).toBe(0);
    expect(comp.totalAccounts()).toBe(0);
  });
});
