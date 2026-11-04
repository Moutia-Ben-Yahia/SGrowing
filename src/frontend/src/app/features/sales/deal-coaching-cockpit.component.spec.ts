import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { DealCoachingCockpitComponent } from './deal-coaching-cockpit.component';

describe('DealCoachingCockpitComponent', () => {
  it('should initialize with active coaching sessions and metrics', () => {
    const comp = new DealCoachingCockpitComponent();
    expect(comp.activeSessionsCount()).toBe(3);
    expect(comp.repAdherenceScore()).toBe(92.8);
    expect(comp.sessions().length).toBeGreaterThan(0);
    expect(comp.objections().length).toBeGreaterThan(0);
  });

  it('should start a new live coaching session', () => {
    const comp = new DealCoachingCockpitComponent();
    const initialCount = comp.sessions().length;
    comp.startNewCoachingSession();
    expect(comp.sessions().length).toBe(initialCount + 1);
    expect(comp.sessions()[comp.sessions().length - 1].accountName).toBe('Starlight Financial Systems');
  });

  it('should verify acoustic objection counter-tactics and evidence proof points', () => {
    const comp = new DealCoachingCockpitComponent();
    const objection = comp.objections()[0];
    expect(objection.category).toBe('PricingAndBudget');
    expect(objection.confidenceScore).toBe(0.96);
    expect(objection.recommendedCounterTactic).toContain('deferred cash terms');
    expect(objection.evidenceProofPoint).toContain('Apex Freight');
  });

  it('should allow updating totalObjectionsQuarter and winRateLiftPct signals', () => {
    const comp = new DealCoachingCockpitComponent();
    comp.totalObjectionsQuarter.set(168);
    comp.winRateLiftPct.set(36.2);

    expect(comp.totalObjectionsQuarter()).toBe(168);
    expect(comp.winRateLiftPct()).toBe(36.2);
  });

  it('should support resetting sessions and objections lists to empty', () => {
    const comp = new DealCoachingCockpitComponent();
    comp.sessions.set([]);
    comp.objections.set([]);
    comp.activeSessionsCount.set(0);

    expect(comp.sessions().length).toBe(0);
    expect(comp.objections().length).toBe(0);
    expect(comp.activeSessionsCount()).toBe(0);
  });
});
