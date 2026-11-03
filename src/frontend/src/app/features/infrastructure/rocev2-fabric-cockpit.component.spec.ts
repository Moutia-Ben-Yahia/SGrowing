import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { Rocev2FabricCockpitComponent } from './rocev2-fabric-cockpit.component';

describe('Rocev2FabricCockpitComponent', () => {
  it('should initialize with connected RoCEv2 GPU nodes', () => {
    const comp = new Rocev2FabricCockpitComponent();
    expect(comp.nodes().length).toBeGreaterThan(0);
    expect(comp.selectedNode()).not.toBeNull();
    expect(comp.selectedNode()?.losslessEnabled).toBe(true);
  });

  it('should detect deadlock when buffer is saturated and trigger drain', () => {
    const comp = new Rocev2FabricCockpitComponent();
    comp.bufferOccupancy = 95;
    comp.simulateTelemetry();

    expect(comp.activeDeadlock()).not.toBeNull();
    expect(comp.activeDeadlock()?.queue).toBe(3);
    expect(comp.activeDeadlock()?.drained).toBe(false);

    comp.drainStuckQueue();
    expect(comp.activeDeadlock()?.drained).toBe(true);
    expect(comp.activeDeadlock()?.mitigationAction).toContain('Queue Drained Successfully');
  });
});
