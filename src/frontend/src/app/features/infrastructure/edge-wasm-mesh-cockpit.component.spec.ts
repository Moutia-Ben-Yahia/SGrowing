// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { EdgeWasmMeshCockpitComponent } from './edge-wasm-mesh-cockpit.component';

describe('EdgeWasmMeshCockpitComponent', () => {
  it('should initialize with Wasm modules and edge invocations', () => {
    const comp = new EdgeWasmMeshCockpitComponent();
    expect(comp.modules().length).toBe(2);
    expect(comp.invocations().length).toBe(1);
    expect(comp.modules()[0].activeEdgePoPsDeployedCount).toBe(310);
  });

  it('should simulate edge invocation and prepend to list', () => {
    const comp = new EdgeWasmMeshCockpitComponent();
    comp.simulateEdgeInvocation();
    expect(comp.invocations().length).toBe(2);
    expect(comp.invocations()[0].edgeLocationIata).toBe('SIN');
    expect(comp.invocations()[0].statusCode).toBe(200);
  });
});
