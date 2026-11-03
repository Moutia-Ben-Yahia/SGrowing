import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { GlobeTopUpTaxCockpitComponent } from './globe-top-up-tax-cockpit.component';

describe('GlobeTopUpTaxCockpitComponent', () => {
  it('should initialize with sovereign jurisdictions and safe-harbor statuses', () => {
    const comp = new GlobeTopUpTaxCockpitComponent();
    expect(comp.jurisdictions().length).toBeGreaterThan(0);
    expect(comp.selectedJurisdiction()).not.toBeNull();
  });

  it('should calculate top-up tax and SBIE carveout for low-tax jurisdiction', () => {
    const comp = new GlobeTopUpTaxCockpitComponent();
    comp.selectJurisdiction(comp.jurisdictions()[0]); // Bermuda
    comp.calculateTopUpTax();

    expect(comp.activeCalculation()).not.toBeNull();
    expect(comp.activeCalculation()?.topUpPct).toBeGreaterThan(10.0);
    expect(comp.activeCalculation()?.topUpTaxEur).toBeGreaterThan(0);
    expect(comp.activeCalculation()?.sbieCarveoutEur).toBeGreaterThan(0);
  });
});
