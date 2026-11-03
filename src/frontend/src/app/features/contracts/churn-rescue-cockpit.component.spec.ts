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
});
