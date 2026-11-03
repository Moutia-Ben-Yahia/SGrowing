// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CapacityPlanningCockpitComponent } from './capacity-planning-cockpit.component';

describe('CapacityPlanningCockpitComponent', () => {
  it('initializes with seed intervals and detects staffing deficit', () => {
    const comp = new CapacityPlanningCockpitComponent();
    expect(comp.plan().hourlyIntervals.length).toBe(4);
    expect(comp.deficitCount()).toBe(1);
    expect(comp.totalVolume()).toBe(720);
    expect(comp.avgSla()).toBeCloseTo(94.1, 1);
  });

  it('rebalances shifts and eliminates all staffing deficits', () => {
    const comp = new CapacityPlanningCockpitComponent();
    expect(comp.deficitCount()).toBe(1);

    comp.rebalanceShifts();
    expect(comp.deficitCount()).toBe(0);
    expect(comp.avgSla()).toBeGreaterThan(97.0);
    expect(comp.plan().hourlyIntervals.find(i => i.hourOfDay === 14)?.hasStaffingDeficit).toBe(false);
  });

  it('simulates volume surge and increases expected volume and deficits', () => {
    const comp = new CapacityPlanningCockpitComponent();
    const initialVolume = comp.totalVolume();

    comp.simulateSurge();
    expect(comp.totalVolume()).toBeGreaterThan(initialVolume);
    expect(comp.deficitCount()).toBeGreaterThanOrEqual(1);
  });
});
