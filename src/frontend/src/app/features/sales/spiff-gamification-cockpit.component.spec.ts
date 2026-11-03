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
});
