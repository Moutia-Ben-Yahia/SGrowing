// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { SreTelemetryCockpitComponent } from './sre-telemetry-cockpit.component';

describe('SreTelemetryCockpitComponent', () => {
  let component: SreTelemetryCockpitComponent;

  beforeEach(() => {
    component = new SreTelemetryCockpitComponent();
  });

  it('should initialize with healthy baseline metrics and empty alerts', () => {
    expect(component).toBeTruthy();
    expect(component.healthScore()).toBe(99.4);
    expect(component.overallStatus()).toBe('HEALTHY');
    expect(component.errorBudgetPercent()).toBe(98.7);
    expect(component.p99LatencyMs()).toBe(38.4);
    expect(component.eventsPerSec()).toBe(10450);
    expect(component.dlqDepth()).toBe(0);
    expect(component.activeAlerts().length).toBe(0);
    expect(component.isDegraded()).toBe(false);
  });

  it('should parse W3C distributed traceparent header into version, traceId, parentId, and flags', () => {
    const trace = component.parsedTrace();
    expect(trace.version).toBe('00');
    expect(trace.traceId).toBe('4bf92f3577b34da6a3ce929d0e0e4736');
    expect(trace.parentId).toBe('00f067aa0ba902b7');
    expect(trace.traceFlags).toBe('01');
    expect(trace.isSampled).toBe(true);
  });

  it('should generate a new synthetic W3C traceparent header with valid format', () => {
    const initialHeader = component.traceParentHeader();
    component.generateNewSyntheticTrace();

    const newHeader = component.traceParentHeader();
    expect(newHeader).not.toBe(initialHeader);
    expect(newHeader.startsWith('00-')).toBe(true);
    expect(newHeader.endsWith('-01')).toBe(true);

    const parts = newHeader.split('-');
    expect(parts.length).toBe(4);
    expect(parts[1].length).toBe(32); // Trace ID hex length
    expect(parts[2].length).toBe(16); // Span ID hex length
  });

  it('should inject synthetic latency and degraded state triggering active SRE alerts', () => {
    expect(component.isDegraded()).toBe(false);

    component.toggleSyntheticDegradation();

    expect(component.overallStatus()).toBe('DEGRADED');
    expect(component.healthScore()).toBe(78.2);
    expect(component.p99LatencyMs()).toBe(412.8);
    expect(component.errorBudgetPercent()).toBe(41.3);
    expect(component.dlqDepth()).toBe(234);
    expect(component.isDegraded()).toBe(true);
    expect(component.activeAlerts().length).toBe(3);

    const critical = component.activeAlerts().find(a => a.severity === 'Critical');
    expect(critical).toBeDefined();
    expect(critical?.summary).toContain('Webhook Ingestion Latency Breach');
  });

  it('should recover from degraded state back to healthy baseline when toggled again', () => {
    // Degrade first
    component.toggleSyntheticDegradation();
    expect(component.overallStatus()).toBe('DEGRADED');

    // Recover
    component.toggleSyntheticDegradation();
    expect(component.overallStatus()).toBe('HEALTHY');
    expect(component.healthScore()).toBe(99.4);
    expect(component.p99LatencyMs()).toBe(38.4);
    expect(component.errorBudgetPercent()).toBe(98.7);
    expect(component.dlqDepth()).toBe(0);
    expect(component.activeAlerts().length).toBe(0);
    expect(component.isDegraded()).toBe(false);
  });

  it('should support acknowledging alerts and draining DLQ backlog', () => {
    component.toggleSyntheticDegradation();
    expect(component.activeAlerts().length).toBe(3);
    expect(component.dlqDepth()).toBe(234);

    // Acknowledge single alert
    const firstAlertId = component.activeAlerts()[0].id;
    component.acknowledgeAlert(firstAlertId);
    expect(component.activeAlerts()[0].acknowledged).toBe(true);

    // Acknowledge all
    component.acknowledgeAllAlerts();
    expect(component.activeAlerts().every(a => a.acknowledged)).toBe(true);

    // Drain DLQ
    component.drainDlq();
    expect(component.dlqDepth()).toBe(0);
    expect(component.activeAlerts().some(a => a.service === 'Dead-Letter-Queue-Worker')).toBe(false);
  });
});
