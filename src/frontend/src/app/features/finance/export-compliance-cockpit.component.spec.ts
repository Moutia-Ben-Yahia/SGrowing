// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ExportComplianceCockpitComponent } from './export-compliance-cockpit.component';

describe('ExportComplianceCockpitComponent', () => {
  it('should initialize with screened transactions, clearance rate, and tokens', () => {
    const comp = new ExportComplianceCockpitComponent();
    expect(comp.totalScreenedTransactions()).toBe(422);
    expect(comp.automatedClearanceRate()).toBe(98.1);
    expect(comp.dualUseItemsTracked()).toBe(20);
    expect(comp.blockedSanctionedAttempts()).toBe(2);
    expect(comp.screenings().length).toBe(2);
    expect(comp.tokens().length).toBe(1);
  });

  it('should screen new export transaction and increment count', () => {
    const comp = new ExportComplianceCockpitComponent();
    const initialScreened = comp.totalScreenedTransactions();
    comp.screenNewTransaction();

    expect(comp.totalScreenedTransactions()).toBe(initialScreened + 1);
    expect(comp.screenings()[2].clearanceStatus).toBe('CLEARED_EXEMPT');
    expect(comp.screenings()[2].destinationCountryIso).toBe('JP');
  });
});
