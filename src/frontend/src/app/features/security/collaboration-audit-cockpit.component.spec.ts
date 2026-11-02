import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CollaborationAuditCockpitComponent } from './collaboration-audit-cockpit.component';

describe('CollaborationAuditCockpitComponent', () => {
  let component: CollaborationAuditCockpitComponent;
  let fixture: ComponentFixture<CollaborationAuditCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CollaborationAuditCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CollaborationAuditCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
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
});
