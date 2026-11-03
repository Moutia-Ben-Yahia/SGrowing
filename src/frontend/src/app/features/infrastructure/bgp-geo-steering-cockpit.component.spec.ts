// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { BgpGeoSteeringCockpitComponent } from './bgp-geo-steering-cockpit.component';

describe('BgpGeoSteeringCockpitComponent', () => {
  it('should initialize with correct default metrics and POP nodes', () => {
    const comp = new BgpGeoSteeringCockpitComponent();
    expect(comp.totalPops()).toBe(4);
    expect(comp.healthyPops()).toBe(3);
    expect(comp.avgLatencyMs()).toBe(34);
    expect(comp.pops().length).toBe(4);
    expect(comp.policies().length).toBe(3);
  });

  it('should optimize latency on global probe trigger', () => {
    const comp = new BgpGeoSteeringCockpitComponent();
    comp.triggerGlobalProbing();
    expect(comp.avgLatencyMs()).toBe(29);
  });

  it('should override DNS policy and record active override', () => {
    const comp = new BgpGeoSteeringCockpitComponent();
    comp.overrideSteeringPolicy();
    expect(comp.activeOverrides()).toBe(1);
    const pol = comp.policies().find(p => p.policyId === 'POL-DNS-01');
    expect(pol?.status).toBe('Overridden');
    expect(pol?.activeEndpointPopId).toBe('POP-FRA-01');
  });
});
