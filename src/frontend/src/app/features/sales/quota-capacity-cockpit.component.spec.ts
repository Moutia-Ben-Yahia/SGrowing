import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { QuotaCapacityCockpitComponent } from './quota-capacity-cockpit.component';

describe('QuotaCapacityCockpitComponent', () => {
  it('should initialize with territories and coverage ratios', () => {
    const comp = new QuotaCapacityCockpitComponent();
    expect(comp.territories().length).toBeGreaterThan(0);
    expect(comp.selectedTerritory()).not.toBeNull();
  });

  it('should rebalance territory headcount and quota per rep', () => {
    const comp = new QuotaCapacityCockpitComponent();
    comp.selectTerritory(comp.territories()[1]); // EMEA-Nordics-UK (8 reps)
    comp.addedHeadcount = 3;
    comp.rebalanceTerritory();

    expect(comp.activeOutcome()).not.toBeNull();
    expect(comp.activeOutcome()?.newHeadcount).toBe(11); // 8 + 3
    expect(comp.activeOutcome()?.newAvgQuotaEur).toBeLessThan(2500000);
  });

  it('should reset active outcome on selecting a different territory and handle empty selection safely', () => {
    const comp = new QuotaCapacityCockpitComponent();
    comp.selectTerritory(comp.territories()[0]);
    expect(comp.selectedTerritory()?.territoryId).toBe('TERR-US-EAST');
    expect(comp.activeOutcome()).toBeNull();

    comp.selectedTerritory.set(null);
    comp.rebalanceTerritory();
    expect(comp.activeOutcome()).toBeNull();
  });

  it('should update territory status to Balanced and adjust coverageRatio upon rebalancing', () => {
    const comp = new QuotaCapacityCockpitComponent();
    const north = comp.territories()[1];
    comp.selectTerritory(north);
    comp.addedHeadcount = 2;
    comp.rebalanceTerritory();

    const updated = comp.territories().find(t => t.territoryId === 'TERR-EMEA-NORTH');
    expect(updated?.status).toBe('Balanced');
    expect(updated?.headcount).toBe(10);
    expect(updated?.coverageRatio).toBe(3.6);
  });

  it('should compute exact rebalance outcome with zero added headcount', () => {
    const comp = new QuotaCapacityCockpitComponent();
    comp.selectTerritory(comp.territories()[0]); // US East, assignedQuotaEur 28,000,000, headcount 14
    comp.addedHeadcount = 0;
    comp.rebalanceTerritory();

    expect(comp.activeOutcome()?.newHeadcount).toBe(14);
    expect(comp.activeOutcome()?.newAvgQuotaEur).toBe(2000000);
    expect(comp.activeOutcome()?.newCoverage).toBe(3.6);
  });
});
