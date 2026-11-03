// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ChaosMeshCockpitComponent } from './chaos-mesh-cockpit.component';

describe('ChaosMeshCockpitComponent', () => {
  it('should initialize with experiments and calculate metrics', () => {
    const comp = new ChaosMeshCockpitComponent();
    expect(comp.experiments().length).toBe(3);
    expect(comp.activeExperimentsCount()).toBe(1);
    expect(comp.haltedCount()).toBe(1);
  });

  it('should select an experiment', () => {
    const comp = new ChaosMeshCockpitComponent();
    const second = comp.experiments()[1];
    comp.selectExperiment(second);
    expect(comp.selectedExperiment()?.targetServiceName).toBe('shard-broker');
  });

  it('should emergency halt active chaos experiment and restore steady state latency', () => {
    const comp = new ChaosMeshCockpitComponent();
    comp.haltExperiment('exp_chaos_01');

    expect(comp.activeExperimentsCount()).toBe(0);
    expect(comp.haltedCount()).toBe(2);

    const updated = comp.experiments().find(e => e.experimentId === 'exp_chaos_01');
    expect(updated?.state).toBe('HaltedEmergencyStop');
    expect(updated?.currentLatencyMs).toBe(22.0);
  });
});
