// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ApiRateLimiterMeshCockpitComponent } from './api-rate-limiter-mesh-cockpit.component';

describe('ApiRateLimiterMeshCockpitComponent', () => {
  it('should initialize with client buckets and quarantined IPs', () => {
    const comp = new ApiRateLimiterMeshCockpitComponent();
    expect(comp.buckets().length).toBe(3);
    expect(comp.quarantinedIps().length).toBe(1);
    expect(comp.buckets()[0].clientId).toBe('CLI-ENT-ACME-01');
  });

  it('should quarantine malicious IP and update list', () => {
    const comp = new ApiRateLimiterMeshCockpitComponent();
    comp.quarantineSampleIp();
    expect(comp.quarantinedIps().length).toBe(2);
    expect(comp.quarantinedIps()[0].ipAddress).toBe('198.51.100.77');
  });
});
