import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { RouteFlapDampingCockpitComponent } from './route-flap-damping-cockpit.component';

describe('RouteFlapDampingCockpitComponent', () => {
  it('should initialize with monitored BGP prefixes and flap history', () => {
    const comp = new RouteFlapDampingCockpitComponent();
    expect(comp.monitoredPrefixesCount()).toBe(2);
    expect(comp.suppressedPrefixesCount()).toBe(1);
    expect(comp.prefixes().length).toBeGreaterThan(0);
    expect(comp.events().length).toBeGreaterThan(0);
  });

  it('should record a new prefix flap event', () => {
    const comp = new RouteFlapDampingCockpitComponent();
    const initialCount = comp.prefixes().length;
    comp.recordPrefixFlap();
    expect(comp.prefixes().length).toBe(initialCount + 1);
    expect(comp.prefixes()[comp.prefixes().length - 1].prefixCidr).toBe('192.0.2.0/24');
  });
});
