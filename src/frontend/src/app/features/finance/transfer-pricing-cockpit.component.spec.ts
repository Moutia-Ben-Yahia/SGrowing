// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TransferPricingCockpitComponent } from './transfer-pricing-cockpit.component';

describe('TransferPricingCockpitComponent', () => {
  it('should initialize with correct default metrics and entities', () => {
    const comp = new TransferPricingCockpitComponent();
    expect(comp.totalEntities()).toBe(4);
    expect(comp.totalVolume()).toBeGreaterThan(2000000);
    expect(comp.complianceRate()).toBe(100.0);
    expect(comp.entities().length).toBe(4);
    expect(comp.invoices().length).toBe(3);
  });

  it('should settle all invoices on FX netting execution', () => {
    const comp = new TransferPricingCockpitComponent();
    comp.executeFxNetting();
    expect(comp.netSettlement()).toBe(0);
    expect(comp.invoices().every(i => i.settlementStatus === 'Settled_NettingPool')).toBe(true);
  });
});
