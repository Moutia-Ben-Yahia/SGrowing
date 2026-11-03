// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { GeoIncrementalityCockpitComponent } from './geo-incrementality-cockpit.component';

describe('GeoIncrementalityCockpitComponent', () => {
  it('should initialize with calibrated cells and experiments', () => {
    const comp = new GeoIncrementalityCockpitComponent();
    expect(comp.cells().length).toBe(2);
    expect(comp.experiments().length).toBe(1);
    expect(comp.experiments()[0].trueIncrementalRoasMultiplier).toBeGreaterThan(2.0);
  });

  it('should launch new geo experiment and prepend to list', () => {
    const comp = new GeoIncrementalityCockpitComponent();
    comp.launchNewGeoExperiment();
    expect(comp.experiments().length).toBe(2);
    expect(comp.experiments()[0].campaignName).toBe('Midwest Metro Omni-Channel Holdout');
    expect(comp.experiments()[0].totalAdSpendUsd).toBe(95000);
  });
});
