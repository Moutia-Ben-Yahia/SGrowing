import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { Section174CockpitComponent } from './section174-cockpit.component';

describe('Section174CockpitComponent', () => {
  it('should initialize with active tax schedules and labor allocations', () => {
    const comp = new Section174CockpitComponent();
    expect(comp.activeSchedulesCount()).toBe(2);
    expect(comp.totalDomesticUsd()).toBe(14500000);
    expect(comp.schedules().length).toBeGreaterThan(0);
    expect(comp.allocations().length).toBeGreaterThan(0);
  });

  it('should create a new Section 174 amortization schedule', () => {
    const comp = new Section174CockpitComponent();
    const initialCount = comp.schedules().length;
    comp.createNewSchedule();
    expect(comp.schedules().length).toBe(initialCount + 1);
    expect(comp.schedules()[comp.schedules().length - 1].taxYear).toBe(2026);
  });
});
