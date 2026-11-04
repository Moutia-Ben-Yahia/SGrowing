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

  it('should evaluate competitor threat levels, deal sizes, and cryptographic battlecard digests', () => {
    const comp = new CompetitiveBattlecardCockpitComponent();
    const highThreat = comp.competitors().find(c => c.threatLevel === 'HIGH');
    expect(highThreat?.competitorName).toBe('ApexCloud Legacy CRM');
    expect(highThreat?.estimatedAverageDealSizeUsd).toBe(320000);

    const initialBattlecard = comp.battlecards()[0];
    expect(initialBattlecard.digest).toMatch(/^0x[0-9A-F]+/);
    expect(initialBattlecard.killPoint).toContain('0-cost migration credit');
  });
});
