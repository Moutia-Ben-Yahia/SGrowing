// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { VendorBillMatchingCockpitComponent } from './vendor-bill-matching-cockpit.component';

describe('VendorBillMatchingCockpitComponent', () => {
  it('initializes with vendor bills and computes match rate and volume', () => {
    const comp = new VendorBillMatchingCockpitComponent();
    expect(comp.bills().length).toBe(4);
    expect(comp.pendingBillsCount()).toBe(3);
    expect(comp.totalBillVolume()).toBe(501500);
    expect(comp.capturedDiscounts()).toBe(4200);
    expect(comp.matchRatePct()).toBe(75.0);
  });

  it('approves early discount on matched bill and captures savings', () => {
    const comp = new VendorBillMatchingCockpitComponent();
    comp.approveDiscount('BILL-2026-4401');

    const updated = comp.bills().find(b => b.billId === 'BILL-2026-4401');
    expect(updated?.status).toBe('ApprovedEarlyDiscount');
    expect(comp.capturedDiscounts()).toBe(7100); // 4200 + 2900
    expect(comp.lastBillNotice()).toContain('Captured $2,900 early discount');
  });
});
