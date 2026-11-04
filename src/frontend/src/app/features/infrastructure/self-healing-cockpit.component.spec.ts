// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { SelfHealingMeshCockpitComponent } from './self-healing-cockpit.component';

describe('SelfHealingMeshCockpitComponent', () => {
  let component: SelfHealingMeshCockpitComponent;

  beforeEach(() => {
    component = new SelfHealingMeshCockpitComponent();
  });

  it('should create component and render active health probes', () => {
    expect(component).toBeTruthy();
    expect(component.probes().length).toBeGreaterThanOrEqual(3);
    expect(component.healthyProbesCount()).toBeGreaterThanOrEqual(3);
  });

  it('should trigger self-healing remediation and optimize latencies', () => {
    component.triggerAutomatedRemediation();
    expect(component.healthyProbesCount()).toBe(component.probes().length);
  });

  it('should inject chaos experiment and verify automated recovery', () => {
    component.selectedChaosType = 'ConnectionExhaustion';
    component.injectChaosExperiment();

    const result = component.chaosResult();
    expect(result).toBeTruthy();
    expect(result?.resilienceGrade).toBe('A+ Resilience');
    expect(result?.blastRadius).toContain('ConnectionExhaustion');
  });

  it('should verify probe properties and initial chaosResult', () => {
    const pgProbe = component.probes().find(p => p.probeId === 'prb_pg_primary');
    expect(pgProbe?.resourceType).toBe('DatabasePool');
    expect(pgProbe?.status).toBe('Healthy');

    const result = component.chaosResult();
    expect(result?.resilienceGrade).toBe('A+ Resilience');
    expect(result?.blastRadius).toContain('network partition');
  });
});
