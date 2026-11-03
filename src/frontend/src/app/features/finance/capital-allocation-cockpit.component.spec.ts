// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CapitalAllocationCockpitComponent } from './capital-allocation-cockpit.component';

describe('CapitalAllocationCockpitComponent', () => {
  it('should initialize with portfolio value, weighted yield, and cash runway', () => {
    const comp = new CapitalAllocationCockpitComponent();
    expect(comp.totalPortfolioValue()).toBe(23500000);
    expect(comp.weightedYieldPct()).toBe(4.71);
    expect(comp.cashRunwayMonths()).toBe(28.5);
    expect(comp.activePositionsCount()).toBe(2);
    expect(comp.allocations().length).toBe(2);
    expect(comp.optimizations().length).toBe(1);
  });

  it('should allocate new capital and increment portfolio value and positions', () => {
    const comp = new CapitalAllocationCockpitComponent();
    const initialPositions = comp.activePositionsCount();
    const initialPortfolio = comp.totalPortfolioValue();
    comp.allocateNewCapital();

    expect(comp.activePositionsCount()).toBe(initialPositions + 1);
    expect(comp.totalPortfolioValue()).toBe(initialPortfolio + 5000000);
    expect(comp.allocations()[initialPositions].assetClass).toBe('SOVEREIGN_TBILLS_USD');
    expect(comp.allocations()[initialPositions].status).toBe('ACTIVE_YIELDING');
  });
});
