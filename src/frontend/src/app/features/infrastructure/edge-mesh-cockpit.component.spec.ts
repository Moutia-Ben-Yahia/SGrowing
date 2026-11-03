// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { EdgeMeshCockpitComponent } from './edge-mesh-cockpit.component';

describe('EdgeMeshCockpitComponent', () => {
  it('initializes with 6 global Edge PoPs and computes latency and warm isolates', () => {
    const comp = new EdgeMeshCockpitComponent();
    expect(comp.pops().length).toBe(6);
    expect(comp.healthyPoPsCount()).toBe(6);
    expect(comp.globalP95Latency()).toBeLessThan(12.0);
    expect(comp.totalWarmIsolates()).toBeGreaterThan(1000);
    expect(comp.functions().length).toBe(3);
  });

  it('toggles PoP health state to simulate fault and recovery', () => {
    const comp = new EdgeMeshCockpitComponent();
    comp.toggleHealth('IAD-01');

    const degraded = comp.pops().find(p => p.popCode === 'IAD-01');
    expect(degraded?.isHealthy).toBe(false);
    expect(comp.healthyPoPsCount()).toBe(5);
    expect(comp.meshNotice()).toContain('Degraded');

    comp.toggleHealth('IAD-01');
    const restored = comp.pops().find(p => p.popCode === 'IAD-01');
    expect(restored?.isHealthy).toBe(true);
    expect(comp.healthyPoPsCount()).toBe(6);
  });

  it('promotes canary edge function to 100% GA across mesh', () => {
    const comp = new EdgeMeshCockpitComponent();
    comp.promote('EDGE-FN-02');

    const promoted = comp.functions().find(f => f.functionId === 'EDGE-FN-02');
    expect(promoted?.canaryTrafficSplitPct).toBe(100);
    expect(promoted?.deploymentState).toBe('PromotedGA');
    expect(comp.meshNotice()).toContain('promoted to 100% GA');
  });
});
