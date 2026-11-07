// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import {
  LoyaltyReferralStudioComponent,
  calculateTier,
  calculateNextTierPointsRemaining,
  getPerksForTier
} from './loyalty-referral-studio.component';

describe('LoyaltyReferralStudioComponent (Domain H4 Loyalty & Referral Ledger)', () => {
  let component: LoyaltyReferralStudioComponent;

  beforeEach(() => {
    component = new LoyaltyReferralStudioComponent();
  });

  it('should create the component with initial seeded VIP Diamond account', () => {
    expect(component).toBeDefined();
    const account = component.account();
    expect(account.contactName).toBe('Sultan Al-Dosari');
    expect(account.currentTier).toBe('VipDiamond');
    expect(account.currentPointsBalance).toBe(4850);
    expect(account.lifetimePointsEarned).toBe(5350);
    expect(account.referralCode).toBe('SULTAN-VIP');
    expect(account.nextTierPointsRemaining).toBe(0);
    expect(account.activeTierPerks.length).toBeGreaterThan(0);
    expect(account.recentTransactions.length).toBe(4);
  });

  it('should initialize KPIs and computed metrics correctly', () => {
    expect(component.activeMembersCount()).toBe(12480);
    expect(component.pointsInCirculation()).toBe(1485200);
    expect(component.redeemedSarValue()).toBe(148520);
    expect(component.referralConversionRate()).toBe('34.8%');
    expect(component.referralShareUrl()).toBe('https://sgrowing.io/ref/SULTAN-VIP');
    expect(component.tierProgressPercent()).toBe(100);
  });

  it('should credit points on earn action and enforce double-entry invariant', () => {
    const initialBalance = component.account().currentPointsBalance;
    const initialLifetime = component.account().lifetimePointsEarned;
    const initialTxCount = component.account().recentTransactions.length;

    component.earnAmount.set(500);
    component.earnType.set('EarnPurchase');
    component.earnReference.set('ORD-TEST-88');
    component.earnDescription.set('Test Ramadan Purchase');

    component.simulateEarn();

    const updated = component.account();
    expect(updated.currentPointsBalance).toBe(initialBalance + 500);
    expect(updated.lifetimePointsEarned).toBe(initialLifetime + 500);
    expect(updated.recentTransactions.length).toBe(initialTxCount + 1);

    // Latest transaction check
    const latestTx = updated.recentTransactions[0];
    expect(latestTx.type).toBe('EarnPurchase');
    expect(latestTx.points).toBe(500);
    expect(latestTx.balanceAfter).toBe(initialBalance + 500);
    // Double-entry invariant verification
    expect(latestTx.balanceAfter).toBe(initialBalance + latestTx.points);

    expect(component.statusMessage()).toContain('Successfully credited 500 points');
    expect(component.errorMessage()).toBeNull();
  });

  it('should burn points on redeem action when balance is sufficient', () => {
    const initialBalance = component.account().currentPointsBalance;
    const initialLifetime = component.account().lifetimePointsEarned;
    const initialTxCount = component.account().recentTransactions.length;

    component.redeemAmount.set(400);
    component.redeemOrderRef.set('ORD-RED-400');

    component.simulateRedeem();

    const updated = component.account();
    expect(updated.currentPointsBalance).toBe(initialBalance - 400);
    expect(updated.lifetimePointsEarned).toBe(initialLifetime); // Lifetime points not decreased!
    expect(updated.recentTransactions.length).toBe(initialTxCount + 1);

    const latestTx = updated.recentTransactions[0];
    expect(latestTx.type).toBe('BurnRedemption');
    expect(latestTx.points).toBe(-400);
    expect(latestTx.balanceAfter).toBe(initialBalance - 400);
    // Double-entry invariant: previous balance + points (-400)
    expect(latestTx.balanceAfter).toBe(initialBalance + latestTx.points);

    expect(component.statusMessage()).toContain('Redeemed 400 points');
    expect(component.errorMessage()).toBeNull();
  });

  it('should guard against redeeming more points than available balance', () => {
    const initialBalance = component.account().currentPointsBalance;
    const initialTxCount = component.account().recentTransactions.length;

    // Attempt to redeem more than available (4850 + 1000 = 5850)
    component.redeemAmount.set(initialBalance + 1000);
    component.redeemOrderRef.set('ORD-OVER-BURN');

    component.simulateRedeem();

    // Verify balance and transactions were NOT modified
    expect(component.account().currentPointsBalance).toBe(initialBalance);
    expect(component.account().recentTransactions.length).toBe(initialTxCount);
    expect(component.errorMessage()).toContain('Insufficient points balance');
    expect(component.statusMessage()).toBeNull();
  });

  it('should reject non-positive earn and redeem amounts', () => {
    component.earnAmount.set(0);
    component.simulateEarn();
    expect(component.errorMessage()).toContain('Points to earn must be greater than zero');

    component.redeemAmount.set(-50);
    component.simulateRedeem();
    expect(component.errorMessage()).toContain('Points to redeem must be greater than zero');
  });

  it('should simulate referral attribution and award dual-sided reward', () => {
    const initialBalance = component.account().currentPointsBalance;
    const initialTxCount = component.account().recentTransactions.length;

    component.refereeContactInput.set('Omar Al-Ghamdi');
    component.refereeOrderTotalSar.set(600); // 250 base + 60 (10% of 600) = 310 pts

    component.simulateReferralAttribution();

    const updated = component.account();
    expect(updated.currentPointsBalance).toBe(initialBalance + 310);
    expect(updated.recentTransactions.length).toBe(initialTxCount + 1);

    const tx = updated.recentTransactions[0];
    expect(tx.type).toBe('EarnReferral');
    expect(tx.points).toBe(310);
    expect(tx.balanceAfter).toBe(initialBalance + 310);
    expect(component.statusMessage()).toContain('Referral converted! Credited +310 points');
  });

  it('should correctly calculate tier progression and thresholds', () => {
    // Threshold tests
    expect(calculateTier(0)).toBe('Bronze');
    expect(calculateTier(499)).toBe('Bronze');
    expect(calculateTier(500)).toBe('Silver');
    expect(calculateTier(1999)).toBe('Silver');
    expect(calculateTier(2000)).toBe('Gold');
    expect(calculateTier(4999)).toBe('Gold');
    expect(calculateTier(5000)).toBe('VipDiamond');
    expect(calculateTier(10000)).toBe('VipDiamond');

    // Next tier remaining points
    expect(calculateNextTierPointsRemaining(250)).toBe(250); // 500 - 250
    expect(calculateNextTierPointsRemaining(500)).toBe(1500); // 2000 - 500
    expect(calculateNextTierPointsRemaining(1200)).toBe(800); // 2000 - 1200
    expect(calculateNextTierPointsRemaining(3000)).toBe(2000); // 5000 - 3000
    expect(calculateNextTierPointsRemaining(5000)).toBe(0);
    expect(calculateNextTierPointsRemaining(7500)).toBe(0);

    // Perks for tier
    expect(getPerksForTier('Bronze').length).toBe(3);
    expect(getPerksForTier('Silver').length).toBe(3);
    expect(getPerksForTier('Gold').length).toBe(4);
    expect(getPerksForTier('VipDiamond').length).toBe(5);
  });

  it('should dynamically update tier progress percent for Bronze/Silver/Gold accounts', () => {
    // Reset account to a new Bronze user with 250 lifetime points
    component.account.set({
      ...component.account(),
      lifetimePointsEarned: 250,
      currentTier: 'Bronze',
      nextTierPointsRemaining: 250
    });

    // 250 / 500 = 50%
    expect(component.tierProgressPercent()).toBe(50);

    // Switch to Silver with 1250 points: (1250 - 500) / 1500 = 750 / 1500 = 50%
    component.account.set({
      ...component.account(),
      lifetimePointsEarned: 1250,
      currentTier: 'Silver',
      nextTierPointsRemaining: 750
    });
    expect(component.tierProgressPercent()).toBe(50);
  });

  it('should trigger referral link copy feedback', () => {
    expect(component.linkCopied()).toBe(false);
    component.copyReferralLink();
    expect(component.linkCopied()).toBe(true);
  });
});
