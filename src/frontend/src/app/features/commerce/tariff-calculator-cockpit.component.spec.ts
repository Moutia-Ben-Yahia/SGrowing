// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TariffCalculatorCockpitComponent } from './tariff-calculator-cockpit.component';

describe('TariffCalculatorCockpitComponent', () => {
  it('initializes with Germany MwSt landed cost and HS 8471.50 classification', () => {
    const comp = new TariffCalculatorCockpitComponent();
    expect(comp.breakdown().hsCode).toBe('8471.50');
    expect(comp.breakdown().country).toBe('DE');
    expect(comp.breakdown().vatRatePct).toBe(19.0);
    expect(comp.breakdown().totalLanded).toBeGreaterThan(2500);
  });

  it('updates landed cost calculation for UK shipment with 20% VAT', () => {
    const comp = new TariffCalculatorCockpitComponent();
    comp.calculateUkLandedCost();
    expect(comp.breakdown().country).toBe('GB');
    expect(comp.breakdown().vatRatePct).toBe(20.0);
    expect(comp.breakdown().totalLanded).toBe(2556.0);
  });
});
