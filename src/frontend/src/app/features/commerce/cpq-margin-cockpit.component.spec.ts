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

  it('computes blended margin and contract value with zero-ramp fallback resilience', () => {
    const comp = new CpqMarginCockpitComponent();
    expect(comp.totalContractValue()).toBe(335891);
    expect(comp.blendedMargin()).toBe(79.5);

    comp.ramps.set([]);
    expect(comp.totalContractValue()).toBe(0);
    expect(comp.blendedMargin()).toBe(0);
  });

  it('should verify termYears and individual year ramp margins in baseline state', () => {
    const comp = new CpqMarginCockpitComponent();
    expect(comp.termYears()).toBe(3);
    const yr1 = comp.ramps().find(r => r.year === 1);
    expect(yr1?.netAcv).toBe(105000);
    expect(yr1?.margin).toBe(78.1);
    expect(comp.auditHash()).toBe('4B8D2A1F9E3C7B5D');
  });
});
