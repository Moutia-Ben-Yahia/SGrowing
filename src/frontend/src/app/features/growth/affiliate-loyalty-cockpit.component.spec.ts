// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { AffiliateLoyaltyCockpitComponent } from './affiliate-loyalty-cockpit.component';

describe('AffiliateLoyaltyCockpitComponent', () => {
  let component: AffiliateLoyaltyCockpitComponent;

  beforeEach(() => {
    component = new AffiliateLoyaltyCockpitComponent();
  });

  it('should initialize with active affiliate partners and calculated commission totals', () => {
    expect(component).toBeTruthy();
    expect(component.partners().length).toBeGreaterThanOrEqual(2);
    expect(component.totalCommissionsEarned()).toBeGreaterThan(10000);
    expect(component.pendingPayoutTotal()).toBeGreaterThan(1000);
  });

  it('should simulate order conversion and calculate commission and points', () => {
    component.simulatedOrderAmount = 5000;
    component.simulateConversion();

    expect(component.simulatedResult()).toContain('$1000 direct commission');
    expect(component.simulatedResult()).toContain('5000 loyalty pts');
  });

  it('should register new affiliate partner and update network list', () => {
    const initial = component.partners().length;
    component.newPartnerName = 'B2B Influencers Hub';
    component.newPartnerCode = 'INFLUENCE25';
    component.newTier1Rate = 25.0;
    component.newTier2Rate = 7.5;

    component.registerPartner();

    expect(component.partners().length).toBe(initial + 1);
    expect(component.partners()[0].code).toBe('INFLUENCE25');
  });

  it('should verify loyalty tiers hierarchy and default points thresholds', () => {
    expect(component.loyaltyTiers.length).toBe(4);
    expect(component.loyaltyTiers[0].tier).toBe('Bronze');
    expect(component.loyaltyTiers[0].pointsRequired).toBe(0);
    expect(component.loyaltyTiers[3].tier).toBe('Platinum');
    expect(component.loyaltyTiers[3].pointsRequired).toBe(50000);
  });

  it('should reset commissions to 0 when partners are cleared and close register modal on submit', () => {
    component.showRegisterModal.set(true);
    component.registerPartner();
    expect(component.showRegisterModal()).toBe(false);

    component.partners.set([]);
    expect(component.totalCommissionsEarned()).toBe(0);
    expect(component.pendingPayoutTotal()).toBe(0);
  });
});
