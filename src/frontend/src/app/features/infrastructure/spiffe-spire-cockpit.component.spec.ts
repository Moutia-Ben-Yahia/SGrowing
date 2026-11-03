// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SpiffeSpireCockpitComponent } from './spiffe-spire-cockpit.component';

describe('SpiffeSpireCockpitComponent', () => {
  it('should initialize with active SVIDs and federated trust domains', () => {
    const comp = new SpiffeSpireCockpitComponent();
    expect(comp.activeSvidsCount()).toBe(2);
    expect(comp.federatedDomainsCount()).toBe(2);
    expect(comp.attestationLatency()).toBe(8.4);
    expect(comp.svids().length).toBe(2);
    expect(comp.federations().length).toBe(2);
  });

  it('should attest workload and issue new active SVID', () => {
    const comp = new SpiffeSpireCockpitComponent();
    const initialSvids = comp.activeSvidsCount();
    comp.attestNewWorkload();

    expect(comp.activeSvidsCount()).toBe(initialSvids + 1);
    expect(comp.svids()[initialSvids].status).toBe('ACTIVE_VALID');
    expect(comp.svids()[initialSvids].spiffeId).toContain('wasm-router');
  });
});
