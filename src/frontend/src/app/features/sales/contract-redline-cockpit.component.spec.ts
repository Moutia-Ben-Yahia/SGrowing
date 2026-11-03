// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ContractRedlineCockpitComponent } from './contract-redline-cockpit.component';

describe('ContractRedlineCockpitComponent', () => {
  it('should initialize with reviewed clauses and pending count', () => {
    const comp = new ContractRedlineCockpitComponent();
    expect(comp.totalReviewed()).toBe(2);
    expect(comp.pendingCount()).toBe(1);
    expect(comp.avgRiskScore()).toBe(40.3);
    expect(comp.redlines().length).toBe(2);
  });

  it('should approve clause deviation and record cryptographic approval seal', () => {
    const comp = new ContractRedlineCockpitComponent();
    const initialApprovals = comp.approvals().length;
    comp.approveClause(comp.redlines()[1]);

    expect(comp.approvals().length).toBe(initialApprovals + 1);
    expect(comp.redlines()[1].status).toBe('APPROVED');
    expect(comp.approvals()[initialApprovals].clauseId).toBe('RED-IND-002');
  });
});
