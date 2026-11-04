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

  it('should allow adding new competitor and updating aggregateWinRatePct', () => {
    const comp = new CompetitiveBattlecardCockpitComponent();
    comp.competitors.update(list => [
      ...list,
      {
        competitorId: 'COMP-SOLAR-03',
        competitorName: 'Solaris Cloud Data',
        marketCategory: 'Cloud Data Warehouse',
        estimatedAverageDealSizeUsd: 450000,
        winRateAgainstPct: 82.0,
        threatLevel: 'LOW'
      }
    ]);
    comp.aggregateWinRatePct.set(78.5);

    expect(comp.competitors().length).toBe(3);
    expect(comp.competitors()[2].competitorId).toBe('COMP-SOLAR-03');
    expect(comp.aggregateWinRatePct()).toBe(78.5);
  });

  it('should support resetting competitors and battlecards lists to empty', () => {
    const comp = new CompetitiveBattlecardCockpitComponent();
    comp.competitors.set([]);
    comp.battlecards.set([]);
    expect(comp.competitors().length).toBe(0);
    expect(comp.battlecards().length).toBe(0);
  });
});
