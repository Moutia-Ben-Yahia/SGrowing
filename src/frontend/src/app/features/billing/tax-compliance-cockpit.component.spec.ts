// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { TaxComplianceCockpitComponent } from './tax-compliance-cockpit.component';

describe('TaxComplianceCockpitComponent', () => {
  let component: TaxComplianceCockpitComponent;

  beforeEach(() => {
    component = new TaxComplianceCockpitComponent();
  });

  it('should initialize with live FX rates and economic nexus jurisdictions', () => {
    expect(component).toBeTruthy();
    expect(component.fxRates.length).toBeGreaterThanOrEqual(4);
    expect(component.nexusList.length).toBeGreaterThanOrEqual(4);
  });

  it('should apply EU reverse charge when B2B with valid VAT ID', () => {
    component.amountBase = 1000;
    component.customerCountry = 'DE';
    component.isB2B = true;
    component.vatNumber = 'DE999888777';

    expect(component.isReverseCharge()).toBe(true);
    expect(component.taxRate()).toBe(0);
    expect(component.taxAmount()).toBe(0);
    expect(component.taxDirective()).toBe('EU-VAT-REVERSE-CHARGE-ART194');
  });

  it('should apply standard VAT when B2C', () => {
    component.amountBase = 1000;
    component.customerCountry = 'DE';
    component.isB2B = false;

    expect(component.isReverseCharge()).toBe(false);
    expect(component.taxRate()).toBe(19);
    expect(component.taxAmount()).toBeGreaterThan(0);
  });
});
