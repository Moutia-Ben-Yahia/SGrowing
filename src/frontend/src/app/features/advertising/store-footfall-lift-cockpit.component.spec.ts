import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { StoreFootfallLiftCockpitComponent } from './store-footfall-lift-cockpit.component';

describe('StoreFootfallLiftCockpitComponent', () => {
  it('should initialize with registered retail stores and polygons', () => {
    const comp = new StoreFootfallLiftCockpitComponent();
    expect(comp.stores().length).toBeGreaterThan(0);
    expect(comp.selectedStore()).not.toBeNull();
    expect(comp.selectedStore()?.dwellMinutes).toBeGreaterThanOrEqual(7);
  });

  it('should calculate incremental footfall lift and CPIV', () => {
    const comp = new StoreFootfallLiftCockpitComponent();
    comp.exposedAudience = 150000;
    comp.calculateLift();

    expect(comp.activeAnalysis()).not.toBeNull();
    expect(comp.activeAnalysis()?.liftPercentage).toBeGreaterThan(20);
    expect(comp.activeAnalysis()?.cpivEur).toBeGreaterThan(0);
  });
});
