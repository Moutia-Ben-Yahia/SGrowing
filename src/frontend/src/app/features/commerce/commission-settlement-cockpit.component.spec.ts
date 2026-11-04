// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CommissionSettlementCockpitComponent } from './commission-settlement-cockpit.component';

describe('CommissionSettlementCockpitComponent', () => {
  it('initializes with commission records and computes totals and escrow', () => {
    const comp = new CommissionSettlementCockpitComponent();
    expect(comp.records().length).toBe(3);
    expect(comp.totalPaid()).toBeGreaterThan(80000);
    expect(comp.totalInEscrow()).toBe(19400); // 14000 + 5400
    expect(comp.avgAttainment()).toBeGreaterThan(100.0);
  });

  it('releases escrow and updates commission status to FullySettled', () => {
    const comp = new CommissionSettlementCockpitComponent();
    comp.releaseEscrow('COMM-2026-101');

    const updated = comp.records().find(r => r.commissionId === 'COMM-2026-101');
    expect(updated?.status).toBe('FullySettled');
    expect(comp.totalInEscrow()).toBe(5400);
    expect(comp.lastReleaseNotice()).toContain('Disbursed $14,000 escrow');
  });

  it('handles unknown commission ids gracefully and completes total settlement flow', () => {
    const comp = new CommissionSettlementCockpitComponent();
    comp.releaseEscrow('NON-EXISTENT-COMMISSION');
    expect(comp.lastReleaseNotice()).toBeNull();
    expect(comp.totalInEscrow()).toBe(19400);

    // Release all remaining escrow records
    comp.releaseEscrow('COMM-2026-101');
    comp.releaseEscrow('COMM-2026-102');
    expect(comp.totalInEscrow()).toBe(0);
  });

  it('should return default 100 attainment when records list is empty', () => {
    const comp = new CommissionSettlementCockpitComponent();
    comp.records.set([]);
    expect(comp.avgAttainment()).toBe(100);
    expect(comp.totalPaid()).toBe(0);
    expect(comp.totalInEscrow()).toBe(0);
  });

  it('should verify rep names and commission tiers across initial records', () => {
    const comp = new CommissionSettlementCockpitComponent();
    const repNames = comp.records().map(r => r.repName);
    expect(repNames).toContain('Victoria Sterling');
    expect(repNames).toContain('Elena Rostova');
    expect(repNames).toContain('Marcus Vance');

    const tiers = comp.records().map(r => r.tier);
    expect(tiers).toContain('SuperAccelerator');
    expect(tiers).toContain('Accelerator');
    expect(tiers).toContain('Standard');
  });
});
