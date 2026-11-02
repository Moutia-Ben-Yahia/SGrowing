import { describe, it, expect, beforeEach } from 'vitest';
import { GrowthTelemetrySignalrService } from './growth-telemetry-signalr.service';

describe('GrowthTelemetrySignalrService (Horizon 6 Real-Time Telemetry Relay)', () => {
  let service: GrowthTelemetrySignalrService;

  beforeEach(() => {
    service = new GrowthTelemetrySignalrService();
  });

  it('should initialize with connected state and null event signals', () => {
    expect(service.isConnected()).toBe(true);
    expect(service.lastIdentityStitched()).toBeNull();
    expect(service.lastCapiConversion()).toBeNull();
    expect(service.lastWorkflowStep()).toBeNull();
    expect(service.lastRoasOptimized()).toBeNull();
  });

  it('should emit identity stitched event and update reactive signal', () => {
    service.emitIdentityStitched('prof_user_99', true, 'Merged 2 sessions');

    const evt = service.lastIdentityStitched();
    expect(evt).not.toBeNull();
    expect(evt!.canonicalProfileId).toBe('prof_user_99');
    expect(evt!.wasMerged).toBe(true);
    expect(evt!.auditMessage).toBe('Merged 2 sessions');
  });

  it('should emit CAPI conversion event and update reactive signal', () => {
    service.emitCapiConversion('Meta CAPI', 'evt_pur_101', 299.99, false);

    const evt = service.lastCapiConversion();
    expect(evt).not.toBeNull();
    expect(evt!.platform).toBe('Meta CAPI');
    expect(evt!.value).toBe(299.99);
    expect(evt!.isDeduplicated).toBe(false);
  });

  it('should emit workflow step telemetry and update reactive signal', () => {
    service.emitWorkflowStep('wf_nurture', 'node_filter', 'Country Filter', 'FilterPassed', 15);

    const evt = service.lastWorkflowStep();
    expect(evt).not.toBeNull();
    expect(evt!.workflowId).toBe('wf_nurture');
    expect(evt!.status).toBe('FilterPassed');
    expect(evt!.latencyMs).toBe(15);
  });
});
