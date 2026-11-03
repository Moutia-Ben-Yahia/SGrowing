// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { PayrollTaxWithholdingCockpitComponent } from './payroll-tax-withholding-cockpit.component';

describe('PayrollTaxWithholdingCockpitComponent', () => {
  it('should initialize with jurisdictions and statutory filings', () => {
    const comp = new PayrollTaxWithholdingCockpitComponent();
    expect(comp.jurisdictions().length).toBe(4);
    expect(comp.filings().length).toBe(2);
    expect(comp.jurisdictions()[0].jurisdictionCode).toBe('US-FED-CA');
  });

  it('should generate filing and add new record to ledger', () => {
    const comp = new PayrollTaxWithholdingCockpitComponent();
    comp.generateFiling();
    expect(comp.filings().length).toBe(3);
    expect(comp.filings()[0].currency).toBe('EUR');
    expect(comp.filings()[0].status).toBe('CONFIRMED_REMITTED');
  });
});
