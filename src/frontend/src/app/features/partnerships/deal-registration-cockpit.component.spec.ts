// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { DealRegistrationCockpitComponent } from './deal-registration-cockpit.component';

describe('DealRegistrationCockpitComponent (Horizon 64)', () => {
  let component: DealRegistrationCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new DealRegistrationCockpitComponent();
  });

  it('should initialize with registered partner deals and calculated metrics', () => {
    expect(component).toBeTruthy();
    expect(component.deals().length).toBeGreaterThanOrEqual(2);
    expect(component.totalArr()).toBeGreaterThan(400000);
    expect(component.lockedCount()).toBe(1);
    expect(component.totalCommissions()).toBeGreaterThan(100000);
  });

  it('should register new partner deal with 90-day exclusivity lock', () => {
    const initialCount = component.deals().length;
    component.registerNewPartnerDeal();
    expect(component.deals().length).toBe(initialCount + 1);
    expect(component.deals()[0].prospectDomain).toBe('helios-energy.ch');
    expect(component.deals()[0].status).toBe('ApprovedLocked');
  });

  it('should approve pending deal registration and update status', () => {
    const pending = component.deals().find(d => d.status === 'PendingReview')!;
    component.approveDeal(pending);
    const updated = component.deals().find(d => d.dealId === pending.dealId)!;
    expect(updated.status).toBe('ApprovedLocked');
  });

  it('should return 0 for ARR, locked count and commissions when deals list is empty', () => {
    component.deals.set([]);
    expect(component.totalArr()).toBe(0);
    expect(component.lockedCount()).toBe(0);
    expect(component.totalCommissions()).toBe(0);
  });
});
