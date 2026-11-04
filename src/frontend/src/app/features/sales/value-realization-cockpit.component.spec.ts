// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ValueRealizationCockpitComponent } from './value-realization-cockpit.component';

describe('ValueRealizationCockpitComponent', () => {
  it('should initialize with accounts, ROI, and QBR decks', () => {
    const comp = new ValueRealizationCockpitComponent();
    expect(comp.monitoredAccountsCount()).toBe(2);
    expect(comp.aggregateRoi()).toBe(2280000);
    expect(comp.avgHealthScore()).toBe(96.5);
    expect(comp.qbrDecksCount()).toBe(1);
    expect(comp.decks().length).toBe(1);
    expect(comp.metrics().length).toBe(2);
  });

  it('should generate new QBR deck and increment deck count', () => {
    const comp = new ValueRealizationCockpitComponent();
    const initialDecks = comp.qbrDecksCount();
    comp.generateNewQbrDeck();

    expect(comp.qbrDecksCount()).toBe(initialDecks + 1);
    expect(comp.decks()[initialDecks].customerAccountName).toBe('Starlight Financial Network');
    expect(comp.decks()[initialDecks].fiscalQuarter).toBe('2026-Q3');
  });

  it('should verify value metric categories, multiplier factors, and audit sources', () => {
    const comp = new ValueRealizationCockpitComponent();
    const metrics = comp.metrics();
    expect(metrics.length).toBe(2);

    const costRed = metrics.find(m => m.objectiveCategory === 'COST_REDUCTION');
    expect(costRed).toBeDefined();
    expect(costRed?.annualizedRoiDollars).toBe(830000);
    expect(costRed?.verificationSource).toBe('AUTOMATED_LEDGER_AUDIT');

    const cycleTime = metrics.find(m => m.objectiveCategory === 'CYCLE_TIME_SPEEDUP');
    expect(cycleTime).toBeDefined();
    expect(cycleTime?.realizationMultiplier).toBe(4.8);
  });
});
