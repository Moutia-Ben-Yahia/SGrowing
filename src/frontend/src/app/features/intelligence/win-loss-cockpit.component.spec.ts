// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { WinLossCockpitComponent } from './win-loss-cockpit.component';

describe('WinLossCockpitComponent', () => {
  it('initializes with seed post-mortems and computes correct win rate', () => {
    const comp = new WinLossCockpitComponent();
    expect(comp.deals().length).toBe(3);
    expect(comp.wonCount()).toBe(2);
    expect(comp.lostCount()).toBe(1);
    expect(comp.winRate()).toBe(67);
    expect(comp.selectedDeal()?.dealId).toBe('deal-wl-101');
  });

  it('filters deals by ClosedWon and ClosedLost outcomes', () => {
    const comp = new WinLossCockpitComponent();
    comp.filterOutcome.set('ClosedWon');
    expect(comp.filteredDeals().length).toBe(2);

    comp.filterOutcome.set('ClosedLost');
    expect(comp.filteredDeals().length).toBe(1);
    expect(comp.filteredDeals()[0].accountName).toBe('Helios Healthcare Cloud');
  });

  it('runs autonomous autopsy, prepends new deal and updates selection and totals', () => {
    const comp = new WinLossCockpitComponent();
    comp.runAutonomousPostMortem();

    expect(comp.deals().length).toBe(4);
    expect(comp.wonCount()).toBe(3);
    expect(comp.winRate()).toBe(75);
    expect(comp.selectedDeal()?.accountName).toBe('Vertex BioPharma Labs');
  });
});
