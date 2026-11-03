// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { IncidentMatrixStudioComponent, IncidentItem } from './incident-matrix-studio.component';

describe('IncidentMatrixStudioComponent (On-Call Incident & Paging Matrix)', () => {
  let component: IncidentMatrixStudioComponent;

  beforeEach(() => {
    component = new IncidentMatrixStudioComponent();
  });

  it('should initialize with default incidents and rosters', () => {
    expect(component).toBeTruthy();
    expect(component.incidents().length).toBe(3);
    expect(component.rosters().length).toBe(2);
    expect(component.activeCount()).toBe(2);
    expect(component.p1Count()).toBe(1);
  });

  it('should filter incidents by selected tab correctly', () => {
    component.selectedTab.set('All');
    expect(component.filteredIncidents().length).toBe(3);

    component.selectedTab.set('Triggered');
    expect(component.filteredIncidents().every(i => i.status === 'Triggered')).toBe(true);

    component.selectedTab.set('Acknowledged');
    expect(component.filteredIncidents().every(i => i.status === 'Acknowledged')).toBe(true);

    component.selectedTab.set('Resolved');
    expect(component.filteredIncidents().every(i => i.status === 'Resolved')).toBe(true);
  });

  it('should acknowledge an incident and update status to Acknowledged', () => {
    const triggered = component.incidents().find(i => i.status === 'Triggered')!;
    expect(triggered).toBeTruthy();

    component.acknowledge(triggered);

    const updated = component.incidents().find(i => i.id === triggered.id)!;
    expect(updated.status).toBe('Acknowledged');
    expect(updated.ackDeadlineSeconds).toBe(0);
  });

  it('should resolve an incident and update status to Resolved', () => {
    const acked = component.incidents().find(i => i.status === 'Acknowledged')!;
    expect(acked).toBeTruthy();

    component.resolve(acked);

    const updated = component.incidents().find(i => i.id === acked.id)!;
    expect(updated.status).toBe('Resolved');
  });

  it('should trigger a new incident and prepend to list', () => {
    const initialCount = component.incidents().length;
    component.openTriggerModal();

    expect(component.incidents().length).toBe(initialCount + 1);
    const newest = component.incidents()[0];
    expect(newest.title).toBe('Database Replica Sync Latency Spillover');
    expect(newest.severity).toBe('P1_Critical');
    expect(newest.status).toBe('Triggered');
  });

  it('should return styled CSS badges for severities and statuses', () => {
    expect(component.getSeverityBadge('P1_Critical')).toContain('bg-rose-500/20');
    expect(component.getSeverityBadge('P2_High')).toContain('bg-amber-500/20');
    expect(component.getSeverityBadge('P3_Medium')).toContain('bg-blue-500/20');
    expect(component.getSeverityBadge('P4_Low')).toContain('bg-slate-700');

    expect(component.getStatusBadge('Triggered')).toContain('bg-rose-600');
    expect(component.getStatusBadge('Acknowledged')).toContain('bg-amber-500/20');
    expect(component.getStatusBadge('Resolved')).toContain('bg-emerald-500/20');
  });
});
