// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { RetailMediaAuctionCockpitComponent } from './retail-media-auction-cockpit.component';

describe('RetailMediaAuctionCockpitComponent', () => {
  it('initializes with auctions and computes revenue and average CPC', () => {
    const comp = new RetailMediaAuctionCockpitComponent();
    expect(comp.auctions().length).toBe(3);
    expect(comp.totalRevenue()).toBeGreaterThan(5000);
    expect(comp.avgClearingCpc()).toBeGreaterThan(1.5);
  });

  it('simulates GSP auction and prepends winning result', () => {
    const comp = new RetailMediaAuctionCockpitComponent();
    comp.simulateAuction();

    expect(comp.auctions().length).toBe(4);
    expect(comp.auctions()[0].winningMerchantName).toBe('ApexWear Health');
    expect(comp.lastAuctionNotice()).toContain('Simulated GSP Auction');
  });
});
