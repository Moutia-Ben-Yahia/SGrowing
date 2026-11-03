// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CiliumEbpfCockpitComponent } from './cilium-ebpf-cockpit.component';

describe('CiliumEbpfCockpitComponent', () => {
  it('should initialize with mesh nodes, network policies, and WireGuard tunnels', () => {
    const comp = new CiliumEbpfCockpitComponent();
    expect(comp.meshNodesCount()).toBe(2);
    expect(comp.networkPoliciesCount()).toBe(2);
    expect(comp.wireGuardTunnelsCount()).toBe(2);
    expect(comp.ebpfLatency()).toBe(12.8);
    expect(comp.policies().length).toBe(2);
    expect(comp.tunnels().length).toBe(2);
  });

  it('should apply new eBPF policy and increment policy count', () => {
    const comp = new CiliumEbpfCockpitComponent();
    const initialPolicies = comp.networkPoliciesCount();
    comp.applyNewPolicy();

    expect(comp.networkPoliciesCount()).toBe(initialPolicies + 1);
    expect(comp.policies()[initialPolicies].status).toBe('ACTIVE_ENFORCED');
    expect(comp.policies()[initialPolicies].namespace).toBe('telephony-voice');
    expect(comp.policies()[initialPolicies].protocol).toBe('UDP');
  });
});
