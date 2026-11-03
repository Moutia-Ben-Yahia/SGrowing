// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { WireGuardDiscoveryCockpitComponent } from './wireguard-discovery-cockpit.component';

describe('WireGuardDiscoveryCockpitComponent', () => {
  it('should initialize with discovered peers, punch rate, and IPsec tunnels', () => {
    const comp = new WireGuardDiscoveryCockpitComponent();
    expect(comp.discoveredPeersCount()).toBe(2);
    expect(comp.directPunchSuccessPct()).toBe(92);
    expect(comp.avgMeshRttMs()).toBe(18.6);
    expect(comp.ipsecFallbackTunnelsCount()).toBe(1);
    expect(comp.peers().length).toBe(2);
    expect(comp.tunnels().length).toBe(1);
  });

  it('should discover new peer and increment peer count', () => {
    const comp = new WireGuardDiscoveryCockpitComponent();
    const initialPeers = comp.discoveredPeersCount();
    comp.discoverNewPeer();

    expect(comp.discoveredPeersCount()).toBe(initialPeers + 1);
    expect(comp.peers()[initialPeers].nodeName).toBe('gcp-europe-west-01');
    expect(comp.peers()[initialPeers].status).toBe('CONNECTED_DIRECT');
  });
});
