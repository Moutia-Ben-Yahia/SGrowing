// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { BidPacingCockpitComponent } from './bid-pacing-cockpit.component';

describe('BidPacingCockpitComponent', () => {
  it('should initialize with campaigns and calculate budget metrics', () => {
    const comp = new BidPacingCockpitComponent();
    expect(comp.campaigns().length).toBe(3);
    expect(comp.totalDailyBudget()).toBe(7800);
    expect(comp.totalSpendToDate()).toBe(5550);
    expect(comp.throttledCount()).toBe(1);
  });

  it('should select a campaign', () => {
    const comp = new BidPacingCockpitComponent();
    const second = comp.campaigns()[1];
    comp.selectCampaign(second);
    expect(comp.selectedCampaign()?.campaignName).toContain('Cloud Migration');
  });

  it('should boost high intent bids and update multiplier and mode', () => {
    const comp = new BidPacingCockpitComponent();
    comp.boostHighIntent('camp_google_high_cpc_02');

    const updated = comp.campaigns().find(c => c.campaignId === 'camp_google_high_cpc_02');
    expect(updated?.currentBidMultiplier).toBe(1.35);
    expect(updated?.mode).toBe('AcceleratedHighIntent');
    expect(comp.throttledCount()).toBe(0);
  });
});
