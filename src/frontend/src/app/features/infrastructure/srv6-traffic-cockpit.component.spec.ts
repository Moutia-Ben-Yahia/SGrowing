import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { Srv6TrafficCockpitComponent } from './srv6-traffic-cockpit.component';

describe('Srv6TrafficCockpitComponent', () => {
  it('should initialize with active SRv6 policies and telemetry', () => {
    const comp = new Srv6TrafficCockpitComponent();
    expect(comp.policiesCount()).toBe(1);
    expect(comp.meshThroughputGbps()).toBe(125.6);
    expect(comp.policies().length).toBeGreaterThan(0);
    expect(comp.telemetry().length).toBeGreaterThan(0);
  });

  it('should create a new SRv6 policy path', () => {
    const comp = new Srv6TrafficCockpitComponent();
    const initialCount = comp.policies().length;
    comp.createPolicyPath();
    expect(comp.policies().length).toBe(initialCount + 1);
    expect(comp.policies()[comp.policies().length - 1].policyName).toBe('Asia-Pacific Core Transit Path');
  });
});
