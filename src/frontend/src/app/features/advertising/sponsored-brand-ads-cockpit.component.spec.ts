import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SponsoredBrandAdsCockpitComponent } from './sponsored-brand-ads-cockpit.component';

describe('SponsoredBrandAdsCockpitComponent', () => {
  it('should initialize with active brand campaigns and winning bids', () => {
    const comp = new SponsoredBrandAdsCockpitComponent();
    expect(comp.activeCampaignsCount()).toBe(1);
    expect(comp.blendedRoasPct()).toBe(420.5);
    expect(comp.campaigns().length).toBeGreaterThan(0);
    expect(comp.bids().length).toBeGreaterThan(0);
  });

  it('should create a new sponsored brand multi-product campaign', () => {
    const comp = new SponsoredBrandAdsCockpitComponent();
    const initialCount = comp.campaigns().length;
    comp.createBrandCampaign();
    expect(comp.campaigns().length).toBe(initialCount + 1);
    expect(comp.campaigns()[comp.campaigns().length - 1].brandName).toBe('LuxeGlow Skincare Organics');
  });

  it('should verify auction bids align with campaign budget constraints and update counts', () => {
    const comp = new SponsoredBrandAdsCockpitComponent();
    const activeCampaign = comp.campaigns()[0];
    const liveBid = comp.bids()[0];

    expect(liveBid.campaignId).toBe(activeCampaign.campaignId);
    expect(liveBid.winningCpcUsd).toBeLessThanOrEqual(activeCampaign.maxCpcBidUsd);
    expect(liveBid.effectiveRoas).toBeGreaterThan(4.0);

    comp.createBrandCampaign();
    comp.createBrandCampaign();

    expect(comp.activeCampaignsCount()).toBe(3);
    expect(comp.campaigns().length).toBe(3);
  });
});
