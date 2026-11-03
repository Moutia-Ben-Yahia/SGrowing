import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SecondaryLiquidityCockpitComponent } from './secondary-liquidity-cockpit.component';

describe('SecondaryLiquidityCockpitComponent', () => {
  it('should initialize with active tender offers and share lots', () => {
    const comp = new SecondaryLiquidityCockpitComponent();
    expect(comp.activeOffersCount()).toBe(1);
    expect(comp.avgSharePriceUsd()).toBe(42.50);
    expect(comp.offers().length).toBeGreaterThan(0);
    expect(comp.lots().length).toBeGreaterThan(0);
  });

  it('should launch a new company tender offer window', () => {
    const comp = new SecondaryLiquidityCockpitComponent();
    const initialCount = comp.offers().length;
    comp.launchNewTenderOffer();
    expect(comp.offers().length).toBe(initialCount + 1);
    expect(comp.offers()[comp.offers().length - 1].programName).toBe('Strategic Secondary Partner Liquidity Window');
  });
});
