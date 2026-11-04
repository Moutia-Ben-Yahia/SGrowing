// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ConsumptionMeteringCockpitComponent } from './consumption-metering-cockpit.component';

describe('ConsumptionMeteringCockpitComponent', () => {
  it('initializes with default metrics and calculates burn rate correctly', () => {
    const comp = new ConsumptionMeteringCockpitComponent();
    expect(comp.profile().tier).toBe('Growth');
    expect(comp.profile().metrics.length).toBe(3);
    // 3500 / 5000 = 70%
    expect(comp.highestBurnRate()).toBe(70);
    expect(comp.projectedOverages()).toBe(0);
    expect(comp.projectedInvoiceTotal()).toBe(499);
  });

  it('ingests traffic spike causing quota breach and overage billing', () => {
    const comp = new ConsumptionMeteringCockpitComponent();
    comp.ingestSimulatedTraffic();

    // 3500 + 2000 = 5500, quota is 5000. Overage: 500 * 0.05 = $25
    expect(comp.projectedOverages()).toBe(25);
    expect(comp.projectedInvoiceTotal()).toBe(524);
    expect(comp.highestBurnRate()).toBe(110);
  });

  it('upgrades subscription tier and expands quotas, reducing burn rate', () => {
    const comp = new ConsumptionMeteringCockpitComponent();
    comp.ingestSimulatedTraffic();
    comp.autoUpgradeCurrentTenant();

    expect(comp.profile().tier).toBe('EnterpriseScale');
    expect(comp.profile().baseFeeMonthlyUsd).toBe(999);
    // Quota doubled to 10000; 5500 / 10000 = 55%
    expect(comp.highestBurnRate()).toBe(55);
    expect(comp.projectedOverages()).toBe(0);
    expect(comp.projectedInvoiceTotal()).toBe(999);
  });

  it('should return 0 burn rate when metric quotaLimit is zero or metrics are empty', () => {
    const comp = new ConsumptionMeteringCockpitComponent();
    expect(comp.getBurnPercent({ metricName: 'Test', consumedUnits: 100, quotaLimit: 0, unitCostUsd: 1, unitLabel: 'calls' })).toBe(0);

    comp.profile.set({ ...comp.profile(), metrics: [] });
    expect(comp.highestBurnRate()).toBe(0);
    expect(comp.projectedOverages()).toBe(0);
    expect(comp.projectedInvoiceTotal()).toBe(comp.profile().baseFeeMonthlyUsd);
  });

  it('should toggle autoUpgradeEnabled in profile', () => {
    const comp = new ConsumptionMeteringCockpitComponent();
    expect(comp.profile().autoUpgradeEnabled).toBe(true);
    comp.profile.update(p => ({ ...p, autoUpgradeEnabled: false }));
    expect(comp.profile().autoUpgradeEnabled).toBe(false);
  });
});
