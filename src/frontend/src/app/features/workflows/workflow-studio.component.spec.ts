// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { WorkflowStudioComponent } from './workflow-studio.component';

describe('WorkflowStudioComponent', () => {
  let component: WorkflowStudioComponent;

  beforeEach(() => {
    vi.useFakeTimers();
    const injector = createEnvironmentInjector([]);
    injector.runInContext(() => {
      component = new WorkflowStudioComponent();
    });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('should initialize with workflow nodes and initial execution logs', () => {
    expect(component).toBeTruthy();
    expect(component.nodes().length).toBe(4);
    expect(component.executionLogs().length).toBe(4);
    expect(component.isRunning()).toBe(false);
  });

  it('should verify workflow node topology and sequence', () => {
    const nodes = component.nodes();
    expect(nodes[0].type).toBe('trigger');
    expect(nodes[0].title).toBe('Inbound Channel Ingress');

    expect(nodes[1].type).toBe('condition');
    expect(nodes[1].title).toBe('AI Sentiment & Intent');

    expect(nodes[2].type).toBe('action');
    expect(nodes[2].title).toBe('Dispatch AI Response');

    expect(nodes[3].type).toBe('action');
    expect(nodes[3].title).toBe('Sync CRM & Alert Team');
  });

  it('should simulate test run and step through active node animation', () => {
    component.runTestSimulation();
    expect(component.isRunning()).toBe(true);

    // Advance 500ms -> step 0 active
    vi.advanceTimersByTime(500);
    expect(component.nodes()[0].active).toBe(true);

    // Advance through all 4 nodes + completion (3000ms total)
    vi.advanceTimersByTime(2500);

    expect(component.isRunning()).toBe(false);
    expect(component.nodes().every(n => !n.active)).toBe(true);

    const logs = component.executionLogs();
    expect(logs.length).toBeGreaterThan(4);
    const lastLog = logs[logs.length - 1];
    expect(lastLog.message).toContain('Interactive test simulation executed all 4 nodes successfully');
  });

  it('should allow adding custom node and updating executionLogs', () => {
    component.nodes.update(list => [...list, {
      id: 'n5',
      type: 'action',
      title: 'Webhook Dispatch',
      subtitle: 'Post payload to external CRM webhook endpoint',
      icon: '🔗',
      color: 'bg-emerald-600/20 text-emerald-400',
      active: false
    }]);
    expect(component.nodes().length).toBe(5);

    component.executionLogs.update(logs => [{
      time: '23:00:00',
      message: '[Custom Log] Execution verified.',
      color: 'text-white'
    }, ...logs]);
    expect(component.executionLogs()[0].message).toContain('[Custom Log]');
  });
});
