// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { PodSandboxingMeshCockpitComponent } from './pod-sandboxing-mesh-cockpit.component';

describe('PodSandboxingMeshCockpitComponent', () => {
  it('should initialize with sandboxed pods and chaos evictions', () => {
    const comp = new PodSandboxingMeshCockpitComponent();
    expect(comp.pods().length).toBe(2);
    expect(comp.evictions().length).toBe(1);
    expect(comp.pods()[0].runtimeClass).toContain('gVisor');
  });

  it('should trigger chaos drill and prepend eviction record', () => {
    const comp = new PodSandboxingMeshCockpitComponent();
    comp.triggerChaosDrill();
    expect(comp.evictions().length).toBe(2);
    expect(comp.evictions()[0].reschedulingLatencyMs).toBeLessThan(500);
  });
});
