import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { IntercompanyNettingCockpitComponent } from './intercompany-netting-cockpit.component';

describe('IntercompanyNettingCockpitComponent', () => {
  it('should initialize with pending intercompany invoices and participants', () => {
    const comp = new IntercompanyNettingCockpitComponent();
    expect(comp.subsidiariesCount()).toBe(3);
    expect(comp.nettingEfficiencyPct()).toBe(80.0);
    expect(comp.invoices().length).toBeGreaterThan(0);
    expect(comp.participants().length).toBeGreaterThan(0);
  });

  it('should add a new intercompany invoice for netting', () => {
    const comp = new IntercompanyNettingCockpitComponent();
    const initialCount = comp.invoices().length;
    comp.addIntercompanyInvoice();
    expect(comp.invoices().length).toBe(initialCount + 1);
    expect(comp.invoices()[comp.invoices().length - 1].debtorName).toBe('SGrowing Japan KK (Tokyo)');
  });
});
