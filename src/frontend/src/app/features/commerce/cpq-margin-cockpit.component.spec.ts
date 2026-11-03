// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CpqMarginCockpitComponent } from './cpq-margin-cockpit.component';

describe('CpqMarginCockpitComponent', () => {
  it('initializes with multi-year ramp and compliant gross margin', () => {
    const comp = new CpqMarginCockpitComponent();
    expect(comp.ramps().length).toBe(3);
    expect(comp.totalContractValue()).toBeGreaterThan(300000);
    expect(comp.blendedMargin()).toBeGreaterThanOrEqual(75.0);
    expect(comp.approvalTier()).toBe('StandardRepApproved');
  });

  it('updates discount and escalates approval tier to VP Sales while maintaining margin floor', () => {
    const comp = new CpqMarginCockpitComponent();
    comp.applyExecutiveConcession();
    expect(comp.discount()).toBe(20.0);
    expect(comp.approvalTier()).toBe('VpSalesApproved');
    expect(comp.blendedMargin()).toBeGreaterThanOrEqual(75.0);
    expect(comp.auditHash()).toBe('9F3A1D7E4C8B2A6F');
  });
});
