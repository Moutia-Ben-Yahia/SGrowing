// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SpatialAdBiddingCockpitComponent } from './spatial-ad-bidding-cockpit.component';

describe('SpatialAdBiddingCockpitComponent', () => {
  it('should initialize with zones and bids', () => {
    const comp = new SpatialAdBiddingCockpitComponent();
    expect(comp.zones().length).toBe(2);
    expect(comp.bids().length).toBe(2);
    expect(comp.bidWinRatePct()).toBeGreaterThan(50);
  });

  it('should simulate spatial bid and prepend winning bid to ledger', () => {
    const comp = new SpatialAdBiddingCockpitComponent();
    comp.simulateSpatialBid();
    expect(comp.bids().length).toBe(3);
    expect(comp.bids()[0].advertiserName).toBe('Hyperion Neural Tech');
    expect(comp.bids()[0].winningBidCpmUsd).toBe(24.50);
  });
});
