// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CabMatrixCockpitComponent } from './cab-matrix-cockpit.component';

describe('CabMatrixCockpitComponent', () => {
  it('should initialize with active CAB members, sponsors, and pairings', () => {
    const comp = new CabMatrixCockpitComponent();
    expect(comp.activeMembersCount()).toBe(3);
    expect(comp.execSponsorsCount()).toBe(3);
    expect(comp.activePairingsCount()).toBe(2);
    expect(comp.alignmentScore()).toBe(94.2);
    expect(comp.pairings().length).toBe(2);
    expect(comp.themes().length).toBe(2);
  });

  it('should align new sponsor pairing and increment count', () => {
    const comp = new CabMatrixCockpitComponent();
    const initialPairings = comp.activePairingsCount();
    comp.alignNewSponsor();

    expect(comp.activePairingsCount()).toBe(initialPairings + 1);
    expect(comp.pairings()[initialPairings].status).toBe('ACTIVE_ALIGNED');
    expect(comp.pairings()[initialPairings].enterpriseAccount).toBe('Nordic Retail Group');
  });
});
