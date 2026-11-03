import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { EvpnVxlanCockpitComponent } from './evpn-vxlan-cockpit.component';

describe('EvpnVxlanCockpitComponent', () => {
  it('should initialize with active VTEP endpoints and tenant VNIs', () => {
    const comp = new EvpnVxlanCockpitComponent();
    expect(comp.activeVtepsCount()).toBe(2);
    expect(comp.meshThroughputGbps()).toBe(184.5);
    expect(comp.vteps().length).toBeGreaterThan(0);
    expect(comp.vnis().length).toBeGreaterThan(0);
  });

  it('should register a new VTEP node into the overlay mesh', () => {
    const comp = new EvpnVxlanCockpitComponent();
    const initialCount = comp.vteps().length;
    comp.registerNewVtep();
    expect(comp.vteps().length).toBe(initialCount + 1);
    expect(comp.vteps()[comp.vteps().length - 1].nodeName).toBe('coreweave-ord1-gpu-01');
  });
});
