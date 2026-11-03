// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { DoohBiddingCockpitComponent } from './dooh-bidding-cockpit.component';

describe('DoohBiddingCockpitComponent', () => {
  it('should initialize with DOOH screens and foot traffic visits', () => {
    const comp = new DoohBiddingCockpitComponent();
    expect(comp.totalScreens()).toBe(2);
    expect(comp.avgMultiplier()).toBe(2.40);
    expect(comp.totalVisits()).toBe(2100);
    expect(comp.placements().length).toBe(2);
    expect(comp.attributions().length).toBe(2);
  });

  it('should submit programmatic DOOH bid and update screens count', () => {
    const comp = new DoohBiddingCockpitComponent();
    const initialScreens = comp.totalScreens();
    comp.submitNewBid();

    expect(comp.totalScreens()).toBe(initialScreens + 1);
    expect(comp.placements()[initialScreens].status).toBe('BID_WON');
    expect(comp.placements()[initialScreens].geoLocationDma).toBe('LON_PICCADILLY');
  });
});
