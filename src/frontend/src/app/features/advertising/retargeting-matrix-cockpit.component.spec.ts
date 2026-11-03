// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { RetargetingMatrixCockpitComponent } from './retargeting-matrix-cockpit.component';

describe('RetargetingMatrixCockpitComponent', () => {
  it('initializes with seed audience segments and correct metrics', () => {
    const comp = new RetargetingMatrixCockpitComponent();
    expect(comp.segments().length).toBe(3);
    expect(comp.activeSegmentCount()).toBe(3);
    expect(comp.pausedSegmentCount()).toBe(0);
    expect(comp.totalAudience()).toBe(144600);
    expect(comp.avgFatigue()).toBe(31);
  });

  it('pauses and resumes a segment correctly', () => {
    const comp = new RetargetingMatrixCockpitComponent();
    const seg = comp.segments()[0];
    comp.togglePause(seg);

    expect(comp.activeSegmentCount()).toBe(2);
    expect(comp.pausedSegmentCount()).toBe(1);
    expect(comp.segments()[0].isPaused).toBe(true);

    comp.togglePause(comp.segments()[0]);
    expect(comp.activeSegmentCount()).toBe(3);
    expect(comp.segments()[0].isPaused).toBe(false);
  });

  it('optimizes frequency caps across all segments and decreases fatigue index', () => {
    const comp = new RetargetingMatrixCockpitComponent();
    const fatigueBefore = comp.avgFatigue();
    comp.optimizeAllFrequencyCaps();

    expect(comp.avgFatigue()).toBeLessThan(fatigueBefore);
    expect(comp.segments()[0].dailyFrequencyCap).toBe(3);
  });

  it('syncs negative exclusions and increments suppressed count', () => {
    const comp = new RetargetingMatrixCockpitComponent();
    const before = comp.negativeExclusionCount();
    comp.syncNegativeExclusions();
    expect(comp.negativeExclusionCount()).toBe(before + 350);
  });
});
