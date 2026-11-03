import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SignageSspCockpitComponent } from './signage-ssp-cockpit.component';

describe('SignageSspCockpitComponent', () => {
  it('should initialize with active display nodes and auctions', () => {
    const comp = new SignageSspCockpitComponent();
    expect(comp.activeNodesCount()).toBe(2);
    expect(comp.totalAuctionsToday()).toBe(1845);
    expect(comp.nodes().length).toBeGreaterThan(0);
    expect(comp.auctions().length).toBeGreaterThan(0);
  });

  it('should register a new retail display node', () => {
    const comp = new SignageSspCockpitComponent();
    const initialCount = comp.nodes().length;
    comp.registerNewDisplayNode();
    expect(comp.nodes().length).toBe(initialCount + 1);
    expect(comp.nodes()[comp.nodes().length - 1].venueStoreName).toBe('Whole Foods Market - Union Square NYC');
  });
});
