// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { InCarAdCockpitComponent } from './in-car-ad-cockpit.component';

describe('InCarAdCockpitComponent', () => {
  it('should initialize with connected vehicles, RTB bidding sessions, and CPM', () => {
    const comp = new InCarAdCockpitComponent();
    expect(comp.activeVehiclesCount()).toBe(2);
    expect(comp.biddingSessionsCount()).toBe(2);
    expect(comp.averageCpm()).toBe(34.50);
    expect(comp.fillRate()).toBe(91.5);
    expect(comp.sessions().length).toBe(2);
    expect(comp.vehicles().length).toBe(2);
  });

  it('should submit new in-car ad bid and increment bidding session count', () => {
    const comp = new InCarAdCockpitComponent();
    const initialSessions = comp.biddingSessionsCount();
    comp.submitInCarBid();

    expect(comp.biddingSessionsCount()).toBe(initialSessions + 1);
    expect(comp.sessions()[initialSessions].auctionResult).toBe('WON');
    expect(comp.sessions()[initialSessions].winningAdvertiserId).toBe('ADV-BP-PULSE');
  });
});
