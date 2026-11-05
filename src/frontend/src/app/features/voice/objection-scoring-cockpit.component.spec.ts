// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ObjectionScoringCockpitComponent } from './objection-scoring-cockpit.component';

describe('ObjectionScoringCockpitComponent (Horizon 58)', () => {
  let component: ObjectionScoringCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new ObjectionScoringCockpitComponent();
  });

  it('should initialize with audited calls and objection stats', () => {
    expect(component).toBeTruthy();
    expect(component.calls().length).toBeGreaterThanOrEqual(2);
    expect(component.avgScore()).toBeGreaterThan(80);
    expect(component.sqlRate()).toBe(100);
  });

  it('should test and update objection rebuttal', () => {
    component.testObjectionRebuttal();
    const rebuttal = component.activeRebuttal();
    expect(rebuttal).toBeTruthy();
    expect(rebuttal?.category).toBe('Budget and Pricing');
    expect(rebuttal?.framing).toContain('Cost of Inaction');
  });

  it('should allow selecting an audited call to inspect coaching notes', () => {
    const secondCall = component.calls()[1];
    component.selectCall(secondCall);
    expect(component.selectedCall()?.callId).toBe(secondCall.callId);
  });

  it('should compute 0 avgScore and 0 sqlRate when calls list is empty', () => {
    component.calls.set([]);
    expect(component.avgScore()).toBe(0);
    expect(component.sqlRate()).toBe(0);
  });

  it('should support updating customUtterance and verify sub-200ms rebuttal latency', () => {
    component.customUtterance = 'We are locked into annual contracts until next year.';
    component.testObjectionRebuttal();

    expect(component.customUtterance).toContain('annual contracts');
    expect(component.activeRebuttal()?.latencyMs).toBeLessThan(200);
    expect(component.activeRebuttal()?.followUps.length).toBeGreaterThanOrEqual(2);
  });
});
