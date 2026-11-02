// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { DagTelemetryStudioComponent } from './dag-telemetry-studio.component';
import { TranslationService } from '../../core/services/translation.service';

describe('DagTelemetryStudioComponent (Domain E1 Workflow DAG Telemetry & Simulator)', () => {
  let component: DagTelemetryStudioComponent;
  let translation: TranslationService;

  beforeEach(() => {
    localStorage.clear();
    translation = new TranslationService();
    translation.setLocale('en-US');
    component = new DagTelemetryStudioComponent(translation);
  });

  it('should initialize with valid acyclic topology and baseline telemetry steps', () => {
    expect(component.nodes().length).toBe(6);
    expect(component.hasCycle()).toBe(false);
    expect(component.telemetrySteps().length).toBe(5);
    expect(component.totalLatencyMs()).toBe(75);
    expect(component.actionsDispatched().length).toBe(2);
  });

  it('should detect and display cycle warning when cycle is injected, and recover when repaired', () => {
    component.injectCycle();
    expect(component.hasCycle()).toBe(true);

    component.repairCycle();
    expect(component.hasCycle()).toBe(false);
  });

  it('should run simulation for VIP contact and execute high-value path to GoalAchieved', () => {
    component.selectedPersona = 'vip';
    component.onPersonaChange();

    component.runSimulation();

    expect(component.telemetrySteps().length).toBe(4);
    expect(component.telemetrySteps().some(s => s.status === 'ConditionMet')).toBe(true);
    expect(component.telemetrySteps().some(s => s.status === 'GoalAchieved')).toBe(true);
    expect(component.actionsDispatched().some(a => a.includes('alert-ae'))).toBe(true);
  });

  it('should run simulation for low-score contact and route to standard path', () => {
    component.selectedPersona = 'low_score';
    component.onPersonaChange();

    component.runSimulation();

    expect(component.telemetrySteps().length).toBe(2);
    expect(component.telemetrySteps()[1].status).toBe('ConditionFailed');
    expect(component.actionsDispatched()[0]).toContain('standard_nurture');
  });
});
