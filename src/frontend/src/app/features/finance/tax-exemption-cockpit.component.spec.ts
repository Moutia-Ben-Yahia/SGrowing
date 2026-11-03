// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { TaxExemptionCockpitComponent } from './tax-exemption-cockpit.component';

describe('TaxExemptionCockpitComponent', () => {
  it('should initialize with exemption certificates and clearances', () => {
    const comp = new TaxExemptionCockpitComponent();
    expect(comp.validCertsCount()).toBe(2);
    expect(comp.clearancesCount()).toBe(2);
    expect(comp.complianceScore()).toBe(99.6);
    expect(comp.certificates().length).toBe(2);
  });

  it('should validate new exemption certificate and append to list', () => {
    const comp = new TaxExemptionCockpitComponent();
    const initialCount = comp.certificates().length;
    comp.validateNewCertificate();

    expect(comp.certificates().length).toBe(initialCount + 1);
    expect(comp.certificates()[initialCount].counterpartyName).toBe('Airbus Defence and Space SAS');
    expect(comp.certificates()[initialCount].status).toBe('VALID');
  });
});
