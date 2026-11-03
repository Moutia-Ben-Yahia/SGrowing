// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { K8sIsolationMeshCockpitComponent } from './k8s-isolation-mesh-cockpit.component';

describe('K8sIsolationMeshCockpitComponent', () => {
  it('initializes with multi-cloud namespaces and counts quarantined pods', () => {
    const comp = new K8sIsolationMeshCockpitComponent();
    expect(comp.namespaces().length).toBe(3);
    expect(comp.quarantinedCount()).toBe(1);
    expect(comp.totalPods()).toBe(46);
  });

  it('toggles eBPF quarantine state on a tenant namespace', () => {
    const comp = new K8sIsolationMeshCockpitComponent();
    comp.toggleQuarantine('ns-tenant-vertex');

    const quarantined = comp.namespaces().find(n => n.namespaceId === 'ns-tenant-vertex');
    expect(quarantined?.isQuarantined).toBe(true);
    expect(quarantined?.level).toBe('HardenedEbpfQuarantine');
    expect(comp.quarantinedCount()).toBe(2);
    expect(comp.lastK8sNotice()).toContain('Quarantined');

    comp.toggleQuarantine('ns-tenant-vertex');
    const restored = comp.namespaces().find(n => n.namespaceId === 'ns-tenant-vertex');
    expect(restored?.isQuarantined).toBe(false);
    expect(restored?.level).toBe('ZeroTrustMtlsEnforced');
  });
});
