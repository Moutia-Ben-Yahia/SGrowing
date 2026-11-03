// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { WasmSandboxCockpitComponent } from './wasm-sandbox-cockpit.component';

describe('WasmSandboxCockpitComponent', () => {
  it('should initialize with running micro-VMs, sub-millisecond cold start, and memory', () => {
    const comp = new WasmSandboxCockpitComponent();
    expect(comp.runningMicroVmsCount()).toBe(2);
    expect(comp.avgColdStartMs()).toBe(0.42);
    expect(comp.activeTenantsCount()).toBe(2);
    expect(comp.totalMemoryAllocatedMb()).toBe(96);
    expect(comp.workers().length).toBe(2);
    expect(comp.capabilities().length).toBe(2);
  });

  it('should deploy new Wasm worker and increment count and memory', () => {
    const comp = new WasmSandboxCockpitComponent();
    const initialWorkers = comp.runningMicroVmsCount();
    const initialMemory = comp.totalMemoryAllocatedMb();
    comp.deployNewWorker();

    expect(comp.runningMicroVmsCount()).toBe(initialWorkers + 1);
    expect(comp.totalMemoryAllocatedMb()).toBe(initialMemory + 48);
    expect(comp.workers()[initialWorkers].status).toBe('RUNNING');
    expect(comp.workers()[initialWorkers].moduleName).toBe('speech-acoustic-filter.wasm');
  });
});
