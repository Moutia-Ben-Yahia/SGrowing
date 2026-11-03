import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SttrTreatyTaxCockpitComponent } from './sttr-treaty-tax-cockpit.component';

describe('SttrTreatyTaxCockpitComponent', () => {
  it('should initialize with intragroup payments and nominal tax rates', () => {
    const comp = new SttrTreatyTaxCockpitComponent();
    expect(comp.payments().length).toBeGreaterThan(0);
    expect(comp.selectedPayment()).not.toBeNull();
  });

  it('should assess payment against 9.0% threshold and compute top-up withholding', () => {
    const comp = new SttrTreatyTaxCockpitComponent();
    comp.selectPayment(comp.payments()[0]); // Bermuda 0% nominal rate
    comp.recipientRate = 0.0;
    comp.assessPayment();

    expect(comp.activeEvaluation()).not.toBeNull();
    expect(comp.activeEvaluation()?.topUpPct).toBe(9.0);
    expect(comp.activeEvaluation()?.withholdingTaxEur).toBe(450000);
    expect(comp.activeEvaluation()?.status).toBe('WithholdingDeductedAndCleared');

    comp.issueCertificate();
    expect(comp.activeEvaluation()?.certificateIssued).toBe(true);
  });
});
