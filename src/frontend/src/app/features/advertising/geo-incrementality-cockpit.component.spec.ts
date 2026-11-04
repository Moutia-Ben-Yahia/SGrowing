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

  it('should validate treatment and synthetic control roles across calibrated cells', () => {
    const comp = new GeoIncrementalityCockpitComponent();
    const treatmentCell = comp.cells().find(c => c.marketRole === 'TEST_TREATMENT');
    const controlCell = comp.cells().find(c => c.marketRole === 'SYNTHETIC_CONTROL');

    expect(treatmentCell).toBeDefined();
    expect(treatmentCell?.dmaRegionName).toContain('Dallas-Fort Worth');
    expect(treatmentCell?.syntheticWeightCoefficient).toBe(1.0);

    expect(controlCell).toBeDefined();
    expect(controlCell?.dmaRegionName).toContain('Chicago Metro');
    expect(controlCell?.syntheticWeightCoefficient).toBe(0.642);

    const totalBaselineRev = comp.cells().reduce((acc, c) => acc + c.baselineWeeklyRevenueUsd, 0);
    expect(totalBaselineRev).toBe(838000);
  });

  it('should compute combined incremental revenue across initial and launched experiments', () => {
    const comp = new GeoIncrementalityCockpitComponent();
    expect(comp.experiments()[0].measuredIncrementalRevenueUsd).toBe(580000);

    comp.launchNewGeoExperiment();
    const totalIncrementalRev = comp.experiments().reduce((sum, e) => sum + e.measuredIncrementalRevenueUsd, 0);
    expect(totalIncrementalRev).toBe(941000); // 580000 + 361000
    expect(comp.experiments()[0].status).toBe('RUNNING_HOLD_OUT');
  });

  it('should allow dynamically adding new test cells to cells signal', () => {
    const comp = new GeoIncrementalityCockpitComponent();
    comp.cells.update(cells => [
      ...cells,
      {
        cellId: 'CELL-DMA-NYC',
        dmaRegionName: 'New York DMA #1',
        marketRole: 'TEST_TREATMENT',
        baselineWeeklyRevenueUsd: 950000,
        syntheticWeightCoefficient: 1.0
      }
    ]);
    expect(comp.cells().length).toBe(3);
    expect(comp.cells()[2].cellId).toBe('CELL-DMA-NYC');
  });
});
