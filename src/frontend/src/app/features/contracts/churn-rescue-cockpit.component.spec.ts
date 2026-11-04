// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ChurnRescueCockpitComponent } from './churn-rescue-cockpit.component';

describe('ChurnRescueCockpitComponent', () => {
  let component: ChurnRescueCockpitComponent;

  beforeEach(() => {
    component = new ChurnRescueCockpitComponent();
  });

  it('should create component and render initial churn risk radar', () => {
    expect(component).toBeTruthy();
    expect(component.accounts().length).toBeGreaterThanOrEqual(3);
    expect(component.highRiskCount()).toBeGreaterThanOrEqual(2);
    expect(component.totalArrAtRisk()).toBeGreaterThan(0);
  });

  it('should execute rescue playbook and reduce churn propensity score', () => {
    const acc = component.accounts()[0];
    const initialScore = acc.churnPropensityScore;

    component.executePlaybook(acc);
    const updated = component.accounts().find(a => a.accountId === acc.accountId);

    expect(updated?.churnPropensityScore).toBeLessThan(initialScore);
    expect(component.rescuedArr()).toBeGreaterThan(339000);
  });

  it('should generate automated contract renewal escrow agreement', () => {
    component.generateRenewalAgreement();
    const agreement = component.renewalAgreement();

    expect(agreement).toBeTruthy();
    expect(agreement?.discountAppliedPercent).toBe(15);
    expect(agreement?.priceLockYears).toBe(2);
    expect(agreement?.signatureStatus).toBe('PendingSignature');
  });

  it('should return correct risk badge classes and handle account selection', () => {
    expect(component.getRiskBadgeClass('SevereCritical')).toContain('bg-rose-900/80');
    expect(component.getRiskBadgeClass('High')).toContain('bg-amber-900/80');
    expect(component.getRiskBadgeClass('Moderate')).toContain('bg-yellow-900/80');
    expect(component.getRiskBadgeClass('Low')).toContain('bg-emerald-900/80');

    const second = component.accounts()[1];
    component.selectAccount(second);
    expect(component.selectedAccount()?.accountId).toBe(second.accountId);
  });

  it('should handle empty accounts and null selectedAccount gracefully', () => {
    component.selectedAccount.set(null);
    const prevAgreement = component.renewalAgreement();
    component.generateRenewalAgreement();
    expect(component.renewalAgreement()).toEqual(prevAgreement);

    component.accounts.set([]);
    expect(component.totalArrAtRisk()).toBe(0);
    expect(component.highRiskCount()).toBe(0);
  });
});
