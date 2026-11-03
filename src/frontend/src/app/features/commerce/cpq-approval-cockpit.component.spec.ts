// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CpqApprovalCockpitComponent } from './cpq-approval-cockpit.component';

describe('CpqApprovalCockpitComponent', () => {
  it('initializes with default quotes and computes KPIs accurately', () => {
    const comp = new CpqApprovalCockpitComponent();
    expect(comp.quotes().length).toBe(4);
    expect(comp.pendingCount()).toBe(3);
    expect(comp.pendingValue()).toBeGreaterThan(500000);
    expect(comp.slaBreaches()).toBe(1);
    expect(comp.avgDiscount()).toBe(8.0);
  });

  it('approves a quote and seals it with HMAC audit token', () => {
    const comp = new CpqApprovalCockpitComponent();
    comp.approve('QUOTE-2026-881');

    const approved = comp.quotes().find(q => q.quoteId === 'QUOTE-2026-881');
    expect(approved?.status).toBe('Approved');
    expect(approved?.auditToken).toMatch(/^CPQ-AUDIT-/);
    expect(comp.lastActionNotice()).toContain('approved and cryptographically sealed');
    expect(comp.pendingCount()).toBe(2);
  });

  it('escalates a quote to CRO / Board Gate with reset SLA', () => {
    const comp = new CpqApprovalCockpitComponent();
    comp.escalate('QUOTE-2026-882');

    const escalated = comp.quotes().find(q => q.quoteId === 'QUOTE-2026-882');
    expect(escalated?.status).toBe('Escalated');
    expect(escalated?.requiredTier).toBe('CroBoardGate');
    expect(escalated?.slaMinutesRemaining).toBe(180);
    expect(comp.lastActionNotice()).toContain('escalated to CRO');
  });
});
