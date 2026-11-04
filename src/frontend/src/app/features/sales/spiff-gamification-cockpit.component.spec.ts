import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SpiffGamificationCockpitComponent } from './spiff-gamification-cockpit.component';

describe('SpiffGamificationCockpitComponent', () => {
  it('should initialize with active campaigns and leaderboard', () => {
    const comp = new SpiffGamificationCockpitComponent();
    expect(comp.activeCampaignsCount()).toBe(1);
    expect(comp.totalEscrowFunded()).toBe(100000);
    expect(comp.campaigns().length).toBeGreaterThan(0);
    expect(comp.leaderboard().length).toBeGreaterThan(0);
  });

  it('should launch a new SPIFF bounty campaign', () => {
    const comp = new SpiffGamificationCockpitComponent();
    const initialCount = comp.campaigns().length;
    comp.launchNewSpiffCampaign();
    expect(comp.campaigns().length).toBe(initialCount + 1);
    expect(comp.campaigns()[comp.campaigns().length - 1].campaignName).toBe('Q1 AI Agent Enterprise Tier Rush');
  });

  it('should verify rep leaderboard ranking and cash payouts', () => {
    const comp = new SpiffGamificationCockpitComponent();
    const rankOne = comp.leaderboard().find(r => r.rank === 1);
    expect(rankOne).toBeDefined();
    expect(rankOne?.salesRepName).toBe('Alexandra Hayes');
    expect(rankOne?.tierBadge).toBe('DIAMOND_CHAMPION');
    expect(rankOne?.totalEarnedUsd).toBe(20000);

    const totalDeals = comp.leaderboard().reduce((sum, r) => sum + r.dealsClosed, 0);
    expect(totalDeals).toBe(14); // 8 + 6
  });

  it('should allow updating spiffsPaidToday and rankedRepsCount signals', () => {
    const comp = new SpiffGamificationCockpitComponent();
    comp.spiffsPaidToday.set(18500);
    comp.rankedRepsCount.set(5);

    expect(comp.spiffsPaidToday()).toBe(18500);
    expect(comp.rankedRepsCount()).toBe(5);
  });

  it('should support resetting campaigns and leaderboard lists to empty', () => {
    const comp = new SpiffGamificationCockpitComponent();
    comp.campaigns.set([]);
    comp.leaderboard.set([]);
    comp.activeCampaignsCount.set(0);

    expect(comp.campaigns().length).toBe(0);
    expect(comp.leaderboard().length).toBe(0);
    expect(comp.activeCampaignsCount()).toBe(0);
  });
});
