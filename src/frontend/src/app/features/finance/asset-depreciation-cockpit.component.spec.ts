// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { AssetDepreciationCockpitComponent } from './asset-depreciation-cockpit.component';

describe('AssetDepreciationCockpitComponent', () => {
  it('should initialize with assets and schedules', () => {
    const comp = new AssetDepreciationCockpitComponent();
    expect(comp.assets().length).toBe(2);
    expect(comp.schedules().length).toBe(1);
    expect(comp.assets()[0].assetTag).toBe('AST-GPU-H100-01');
    expect(comp.assets()[0].costBasisUsd).toBe(2500000);
  });

  it('should calculate depreciation schedule and prepend entry', () => {
    const comp = new AssetDepreciationCockpitComponent();
    comp.calculateDepreciationSchedule();
    expect(comp.schedules().length).toBe(2);
    expect(comp.schedules()[0].assetTag).toBe('AST-EDGE-POP-02');
    expect(comp.schedules()[0].depreciationAmountUsd).toBe(12750.00);
  });
});
