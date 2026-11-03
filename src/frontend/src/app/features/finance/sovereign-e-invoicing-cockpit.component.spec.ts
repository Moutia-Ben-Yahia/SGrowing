// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SovereignEInvoicingCockpitComponent } from './sovereign-e-invoicing-cockpit.component';

describe('SovereignEInvoicingCockpitComponent', () => {
  it('should initialize with portals and invoices', () => {
    const comp = new SovereignEInvoicingCockpitComponent();
    expect(comp.portals().length).toBe(4);
    expect(comp.invoices().length).toBe(2);
    expect(comp.portals()[0].portalCode).toBe('PEPPOL_EU');
  });

  it('should generate clearance invoice and prepend cleared invoice', () => {
    const comp = new SovereignEInvoicingCockpitComponent();
    comp.generateClearanceInvoice();
    expect(comp.invoices().length).toBe(3);
    expect(comp.invoices()[0].portalCode).toBe('CHORUS_PRO_FR');
    expect(comp.invoices()[0].status).toBe('CLEARED_BY_TAX_AUTHORITY');
  });
});
