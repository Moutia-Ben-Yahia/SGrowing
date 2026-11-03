// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { EbpfSecurityMeshCockpitComponent } from './ebpf-security-mesh-cockpit.component';

describe('EbpfSecurityMeshCockpitComponent', () => {
  it('should initialize with active probes and violations', () => {
    const comp = new EbpfSecurityMeshCockpitComponent();
    expect(comp.probes().length).toBe(2);
    expect(comp.violations().length).toBe(1);
    expect(comp.probes()[0].hookedSyscallsCount).toBe(128);
  });

  it('should simulate lateral threat block and prepend violation to list', () => {
    const comp = new EbpfSecurityMeshCockpitComponent();
    comp.simulateLateralThreatBlock();
    expect(comp.violations().length).toBe(2);
    expect(comp.violations()[0].enforcementAction).toBe('KERNEL_DROP');
    expect(comp.violations()[0].sourcePod).toBe('untrusted-crawler-pod-88');
  });
});
