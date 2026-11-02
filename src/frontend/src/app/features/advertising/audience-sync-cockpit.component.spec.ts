// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { AudienceSyncCockpitComponent } from './audience-sync-cockpit.component';

describe('AudienceSyncCockpitComponent (Horizon 57)', () => {
  let component: AudienceSyncCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new AudienceSyncCockpitComponent();
  });

  it('should initialize with connected DSP platforms', () => {
    expect(component).toBeTruthy();
    expect(component.connectors().length).toBeGreaterThanOrEqual(4);
    expect(component.connectedDspsCount()).toBe(4);
    expect(component.totalHashedRecords()).toBeGreaterThan(1000000);
  });

  it('should stream new cohort sync job', () => {
    const initialJobs = component.syncJobs().length;
    component.streamCohortSync();
    expect(component.syncJobs().length).toBe(initialJobs + 1);
    expect(component.syncJobs()[0].status).toBe('StreamedToDsp');
  });

  it('should retry failed or buffered sync chunk', () => {
    const initialJobs = component.syncJobs().length;
    component.retryFailedSync();
    expect(component.syncJobs().length).toBe(initialJobs + 1);
    expect(component.syncJobs()[0].status).toBe('RecoveredAndStreamed');
  });
});
