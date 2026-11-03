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
});
