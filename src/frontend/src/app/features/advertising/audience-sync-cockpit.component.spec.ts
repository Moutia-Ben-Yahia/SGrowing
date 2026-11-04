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

  it('should compute average latency across all connected DSP platforms', () => {
    const latency = component.avgLatency();
    expect(latency).toBeGreaterThan(100);
    expect(latency).toBeLessThan(500);

    const metaConn = component.connectors().find(c => c.platform === 'MetaCustomAudience');
    expect(metaConn?.isConnected).toBe(true);
    expect(metaConn?.syncedAudiences).toBeGreaterThan(0);
  });

  it('should compute 0 avgLatency, 0 hashed records, and 0 connected DSPs when connectors list is empty', () => {
    component.connectors.set([]);
    expect(component.avgLatency()).toBe(0);
    expect(component.totalHashedRecords()).toBe(0);
    expect(component.connectedDspsCount()).toBe(0);
  });
});
