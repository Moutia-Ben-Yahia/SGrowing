import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { SelfHealingMeshCockpitComponent } from './self-healing-cockpit.component';

describe('SelfHealingMeshCockpitComponent', () => {
  let component: SelfHealingMeshCockpitComponent;
  let fixture: ComponentFixture<SelfHealingMeshCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SelfHealingMeshCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(SelfHealingMeshCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
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
});
