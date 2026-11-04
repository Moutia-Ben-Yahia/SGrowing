// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ExecutiveRevenueCockpitComponent } from './executive-revenue-cockpit.component';

describe('ExecutiveRevenueCockpitComponent', () => {
  let component: ExecutiveRevenueCockpitComponent;

  beforeEach(() => {
    component = new ExecutiveRevenueCockpitComponent();
  });

  it('should initialize revenue KPIs', () => {
    expect(component).toBeTruthy();
    expect(component.arr()).toBeGreaterThan(1000000);
    expect(component.mrr()).toBeGreaterThan(100000);
    expect(component.nrr()).toBeGreaterThan(100);
  });

  it('should have valid waterfall metrics', () => {
    const wf = component.waterfall();
    expect(wf.netEndingMrr).toBe(wf.startingMrr + wf.newMrr + wf.expansionMrr - wf.contractionMrr - wf.churnedMrr);
    expect(wf.quickRatio).toBeGreaterThan(1.5);
  });

  it('should render cohort retention rows', () => {
    expect(component.cohortRows.length).toBe(6);
    expect(component.cohortRows[0].retention[0]).toBe(100);
  });

  it('should return correct retention color styles and remaining cols padding', () => {
    expect(component.getRetentionColor(98)).toContain('bg-emerald-100');
    expect(component.getRetentionColor(92)).toContain('bg-emerald-50');
    expect(component.getRetentionColor(87)).toContain('bg-amber-50');
    expect(component.getRetentionColor(80)).toContain('bg-rose-50');

    expect(component.getRemainingCols(4).length).toBe(2);
    expect(component.getRemainingCols(6).length).toBe(0);
  });

  it('should allow modifying arr, mrr, nrr and grr signals', () => {
    component.arr.set(2000000);
    component.mrr.set(166667);
    component.nrr.set(125.0);
    component.grr.set(97.5);

    expect(component.arr()).toBe(2000000);
    expect(component.mrr()).toBe(166667);
    expect(component.nrr()).toBe(125.0);
    expect(component.grr()).toBe(97.5);
  });
});
