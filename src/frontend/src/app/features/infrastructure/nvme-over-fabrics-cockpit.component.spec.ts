import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { NvmeOverFabricsCockpitComponent } from './nvme-over-fabrics-cockpit.component';

describe('NvmeOverFabricsCockpitComponent', () => {
  it('should initialize with active NVMe-oF subsystems and ANA states', () => {
    const comp = new NvmeOverFabricsCockpitComponent();
    expect(comp.subsystems().length).toBeGreaterThan(0);
    expect(comp.selectedSubsystem()).not.toBeNull();
    expect(comp.selectedSubsystem()?.anaState).toBe('Optimized');
  });

  it('should simulate ANA multipath failover under 10ms with zero data loss', () => {
    const comp = new NvmeOverFabricsCockpitComponent();
    comp.simulateAnaFailover();

    expect(comp.lastFailover()).not.toBeNull();
    expect(comp.lastFailover()?.durationMs).toBeLessThanOrEqual(10);
    expect(comp.lastFailover()?.zeroDataLoss).toBe(true);
    expect(comp.selectedSubsystem()?.anaState).toBe('FailoverActive');
  });
});
