// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CompetitiveBattlecardCockpitComponent } from './competitive-battlecard-cockpit.component';

describe('CompetitiveBattlecardCockpitComponent', () => {
  it('should initialize with tracked rivals and battlecards', () => {
    const comp = new CompetitiveBattlecardCockpitComponent();
    expect(comp.competitors().length).toBe(2);
    expect(comp.battlecards().length).toBe(1);
    expect(comp.aggregateWinRatePct()).toBeGreaterThan(50);
  });

  it('should generate dynamic battlecard and prepend to list', () => {
    const comp = new CompetitiveBattlecardCockpitComponent();
    comp.generateLiveBattlecard();
    expect(comp.battlecards().length).toBe(2);
    expect(comp.battlecards()[0].competitorName).toBe('Titanium Marketing Cloud');
    expect(comp.battlecards()[0].killPoint).toContain('identity stitching');
  });
});
