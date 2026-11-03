// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { InfluencerEscrowV2CockpitComponent } from './influencer-escrow-v2-cockpit.component';

describe('InfluencerEscrowV2CockpitComponent', () => {
  it('should initialize with campaigns and correct escrow totals', () => {
    const comp = new InfluencerEscrowV2CockpitComponent();
    expect(comp.campaigns().length).toBe(3);
    expect(comp.totalEscrowLocked()).toBe(35500);
    expect(comp.totalGmv()).toBe(147600);
    expect(comp.releasedCampaignsCount()).toBe(0);
  });

  it('should select an escrow campaign', () => {
    const comp = new InfluencerEscrowV2CockpitComponent();
    const second = comp.campaigns()[1];
    comp.selectCampaign(second);
    expect(comp.selectedCampaign()?.creatorHandle).toBe('@growth_hacker_sarah');
  });

  it('should release escrow funds and decrement locked balance', () => {
    const comp = new InfluencerEscrowV2CockpitComponent();
    comp.releaseEscrow('escrow_camp_01');
    expect(comp.releasedCampaignsCount()).toBe(1);
    expect(comp.totalEscrowLocked()).toBe(20500); // 35500 - 15000

    const updated = comp.campaigns().find(c => c.campaignId === 'escrow_camp_01');
    expect(updated?.status).toBe('MilestoneReleased');
  });
});
