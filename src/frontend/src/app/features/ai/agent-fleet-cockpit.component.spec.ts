// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { TranslationService } from '../../core/services/translation.service';
import { AgentFleetCockpitComponent } from './agent-fleet-cockpit.component';

describe('AgentFleetCockpitComponent', () => {
  let component: AgentFleetCockpitComponent;

  beforeEach(() => {
    vi.useFakeTimers();

    const mockTranslation = {
      t: (key: string) => key,
      currentLanguage: signal('en'),
      setLanguage: vi.fn()
    };

    const injector = Injector.create({
      providers: [
        { provide: TranslationService, useValue: mockTranslation }
      ]
    });

    component = runInInjectionContext(injector, () => new AgentFleetCockpitComponent());
  });

  it('should initialize with full 38 dual-layer agent personas', () => {
    expect(component).toBeTruthy();
    expect(component.personas().length).toBe(38);
    expect(component.activeCount()).toBe(38);
    expect(component.anomaliesCount()).toBeGreaterThan(0);
  });

  it('should filter personas by business vs engineering layers', () => {
    component.filterLayer.set('Business');
    const businessPersonas = component.filteredPersonas();
    expect(businessPersonas.length).toBe(17);
    expect(businessPersonas.every(p => p.layer === 'Business')).toBe(true);

    component.filterLayer.set('Engineering');
    const engineeringPersonas = component.filteredPersonas();
    expect(engineeringPersonas.length).toBe(21);
    expect(engineeringPersonas.every(p => p.layer === 'Engineering')).toBe(true);
  });

  it('should execute autonomous heartbeat sweep and update findings', () => {
    component.runHeartbeatSweep();
    expect(component.isSweeping()).toBe(true);

    vi.advanceTimersByTime(1300);
    expect(component.isSweeping()).toBe(false);
    expect(component.findings()[0].agentName).toBe('RevOps Strategist');
  });

  it('should execute automated self-healing remediation', () => {
    component.runRemediation();
    expect(component.isRemediating()).toBe(true);

    vi.advanceTimersByTime(1000);
    expect(component.isRemediating()).toBe(false);
    expect(component.findings()[0].agentName).toBe('Logistics Controller');
  });

  it('should execute simulated agent task with prompt', () => {
    const agent = component.personas()[0];
    component.runAgentTask(agent);
    expect(component.activeModalAgent()).toBe(agent);

    component.taskPrompt = 'Audit spatial inventory buffers in Tunis depot';
    component.executeTask();
    expect(component.isTaskRunning()).toBe(true);

    vi.advanceTimersByTime(900);
    expect(component.isTaskRunning()).toBe(false);
    expect(component.taskOutput()).toContain('Task execution complete');
  });
});
