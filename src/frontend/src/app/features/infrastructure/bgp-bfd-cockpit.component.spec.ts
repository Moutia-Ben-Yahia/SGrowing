// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { BgpBfdCockpitComponent } from './bgp-bfd-cockpit.component';

describe('BgpBfdCockpitComponent', () => {
  it('should initialize with BFD sessions and health index', () => {
    const comp = new BgpBfdCockpitComponent();
    expect(comp.totalSessions()).toBe(3);
    expect(comp.healthySessions()).toBe(3);
    expect(comp.healthIndex()).toBe(100.0);
    expect(comp.failoverTarget()).toBe(800.0);
    expect(comp.sessions().length).toBe(3);
  });

  it('should simulate BFD failover and restore session', () => {
    const comp = new BgpBfdCockpitComponent();
    const initialHealthy = comp.healthySessions();
    comp.simulateFailover(comp.sessions()[0]);

    expect(comp.healthySessions()).toBe(initialHealthy - 1);
    expect(comp.sessions()[0].sessionState).toBe('POISONED');
    expect(comp.events()[0].actionTaken).toBe('ROUTE_WITHDRAWN');

    comp.restoreSession(comp.sessions()[0]);
    expect(comp.healthySessions()).toBe(initialHealthy);
    expect(comp.sessions()[0].sessionState).toBe('UP');
    expect(comp.events()[0].actionTaken).toBe('ROUTE_ANNOUNCED');
  });
});
