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

  it('should verify deviation risk classification boundaries and zero pending count upon approval', () => {
    const comp = new ContractRedlineCockpitComponent();
    expect(comp.redlines().every(r => r.riskClassification === 'ACCEPTABLE_COMPROMISE')).toBe(true);
    expect(comp.redlines().every(r => r.deviationRiskScore < 50.0)).toBe(true);

    const pendingClause = comp.redlines().find(r => r.status === 'PENDING_REVIEW');
    expect(pendingClause).toBeDefined();

    comp.approveClause(pendingClause!);
    expect(comp.pendingCount()).toBe(0);
    expect(comp.approvals().some(a => a.approvedByLegalCounsel.includes('Chief Legal Officer'))).toBe(true);
  });

  it('should allow updating avgRiskScore and compromisesCount signals', () => {
    const comp = new ContractRedlineCockpitComponent();
    comp.avgRiskScore.set(28.5);
    comp.compromisesCount.set(3);

    expect(comp.avgRiskScore()).toBe(28.5);
    expect(comp.compromisesCount()).toBe(3);
  });

  it('should not allow pendingCount to decrease below 0 when approving multiple clauses', () => {
    const comp = new ContractRedlineCockpitComponent();
    comp.pendingCount.set(0);
    comp.approveClause(comp.redlines()[0]);
    expect(comp.pendingCount()).toBe(0);
  });
});
