// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { FxHedgingCockpitComponent } from './fx-hedging-cockpit.component';

describe('FxHedgingCockpitComponent', () => {
  it('initializes with seed contracts and computes hedged notional', () => {
    const comp = new FxHedgingCockpitComponent();
    expect(comp.contracts().length).toBe(2);
    expect(comp.activeContractsCount()).toBe(2);
    expect(comp.settledCount()).toBe(0);
    // 271250 + 233100 = 504350
    expect(comp.totalHedgedUsd()).toBe(504350);
  });

  it('settles forward contract and increments cumulative savings', () => {
    const comp = new FxHedgingCockpitComponent();
    const c1 = comp.contracts()[0];
    const savingsBefore = comp.cumulativeSavingsUsd();

    comp.settleContract(c1);
    expect(comp.activeContractsCount()).toBe(1);
    expect(comp.settledCount()).toBe(1);
    expect(comp.cumulativeSavingsUsd()).toBe(savingsBefore + 4800);
    expect(comp.totalHedgedUsd()).toBe(233100);
  });

  it('locks a new forward contract and prepends to contracts list', () => {
    const comp = new FxHedgingCockpitComponent();
    const countBefore = comp.contracts().length;

    comp.lockNewForward();
    expect(comp.contracts().length).toBe(countBefore + 1);
    expect(comp.contracts()[0].corridor).toBe('SGD/USD');
    expect(comp.contracts()[0].lockedExchangeRate).toBe(0.7650);
  });
});
