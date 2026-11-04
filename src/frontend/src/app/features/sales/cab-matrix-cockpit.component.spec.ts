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

  it('should verify high executive alignment scores and strategic co-innovation themes', () => {
    const comp = new CabMatrixCockpitComponent();
    expect(comp.pairings().every(p => p.alignmentScore > 90.0)).toBe(true);

    const totalVotes = comp.themes().reduce((sum, t) => sum + t.votesCount, 0);
    expect(totalVotes).toBe(14); // 8 + 6

    const coreTheme = comp.themes().find(t => t.category === 'Product Core');
    expect(coreTheme?.title).toContain('Autonomous AI Multi-Agent');
  });

  it('should allow adding new strategic theme and updating alignment score', () => {
    const comp = new CabMatrixCockpitComponent();
    comp.themes.update(list => [
      ...list,
      {
        themeId: 'THEME-003',
        title: 'Zero-Trust Credential Mesh',
        category: 'Security & Auth',
        votesCount: 11,
        totalScore: 92.0
      }
    ]);
    comp.alignmentScore.set(96.8);

    expect(comp.themes().length).toBe(3);
    expect(comp.themes()[2].themeId).toBe('THEME-003');
    expect(comp.alignmentScore()).toBe(96.8);
  });
});
