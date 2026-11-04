// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { CollaborationAuditCockpitComponent } from './collaboration-audit-cockpit.component';

describe('CollaborationAuditCockpitComponent', () => {
  let component: CollaborationAuditCockpitComponent;

  beforeEach(() => {
    component = new CollaborationAuditCockpitComponent();
  });

  it('should initialize with active collaborators and computed critical logs count', () => {
    expect(component).toBeTruthy();
    expect(component.collaborators().length).toBe(2);
    expect(component.auditLogs().length).toBe(2);
    expect(component.criticalCount()).toBe(1);
    expect(component.geofenceAlerts()).toBe(1);
  });

  it('should simulate collaborator heartbeat and prepend to list', () => {
    const initialCount = component.collaborators().length;
    component.simulateHeartbeat();

    expect(component.collaborators().length).toBe(initialCount + 1);
    expect(component.collaborators()[0].name).toContain('Elena Rostova');
  });

  it('should revoke compromised sessions and log Critical audit event', () => {
    const initialLogCount = component.auditLogs().length;
    component.revokeCompromisedSession();

    expect(component.auditLogs().length).toBe(initialLogCount + 1);
    expect(component.auditLogs()[0].action).toBe('UserSessionsTerminated');
    expect(component.auditLogs()[0].severity).toBe('Critical');
  });

  it('should return 0 criticalCount when auditLogs list is empty', () => {
    component.auditLogs.set([]);
    expect(component.criticalCount()).toBe(0);
    expect(component.auditLogs().length).toBe(0);
  });

  it('should accurately compute criticalCount when adding Info, Warning and Critical logs', () => {
    component.auditLogs.set([]);
    expect(component.criticalCount()).toBe(0);

    component.auditLogs.set([
      { id: '1', user: 'u1', action: 'Login', target: 'App', country: 'US', severity: 'Info', timestamp: '1m ago' },
      { id: '2', user: 'u2', action: 'PasswordChange', target: 'User', country: 'FR', severity: 'Warning', timestamp: '2m ago' }
    ]);
    expect(component.criticalCount()).toBe(0);

    component.auditLogs.update(logs => [
      { id: '3', user: 'u3', action: 'BreachAttempt', target: 'Vault', country: 'XX', severity: 'Critical', timestamp: 'now' },
      ...logs
    ]);
    expect(component.criticalCount()).toBe(1);
  });
});
