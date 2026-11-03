// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TaxNexusCockpitComponent } from './tax-nexus-cockpit.component';

describe('TaxNexusCockpitComponent', () => {
  it('should initialize with jurisdictions and calculate metrics', () => {
    const comp = new TaxNexusCockpitComponent();
    expect(comp.jurisdictions().length).toBe(5);
    expect(comp.registrationRequiredCount()).toBe(1);
    expect(comp.activeCollectingCount()).toBe(1);
    expect(comp.totalTaxLiability()).toBe(111850);
  });

  it('should select a jurisdiction', () => {
    const comp = new TaxNexusCockpitComponent();
    const first = comp.jurisdictions()[0];
    comp.selectJurisdiction(first);
    expect(comp.selectedJurisdiction()?.jurisdictionCode).toBe('US-CA');
  });

  it('should register and collect taxes in breached jurisdiction', () => {
    const comp = new TaxNexusCockpitComponent();
    comp.registerAndCollect('US-NY');

    expect(comp.registrationRequiredCount()).toBe(0);
    expect(comp.activeCollectingCount()).toBe(2);

    const updated = comp.jurisdictions().find(j => j.jurisdictionCode === 'US-NY');
    expect(updated?.state).toBe('RegisteredAndCollecting');
  });
});
