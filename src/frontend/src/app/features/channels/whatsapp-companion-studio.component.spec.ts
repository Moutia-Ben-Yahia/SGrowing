// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { WhatsAppCompanionStudioComponent, CompanionSession } from './whatsapp-companion-studio.component';
import { of } from 'rxjs';

describe('WhatsAppCompanionStudioComponent', () => {
  let component: WhatsAppCompanionStudioComponent;
  let httpMock: any;

  const mockSession: CompanionSession = {
    sessionId: 'sess-wa-101',
    tenantId: 'tenant-wa-44',
    phoneNumber: '+216 71 998 801',
    deviceModel: 'Ubuntu Multi-Device Daemon',
    state: 'Connected',
    pairingCode: '4892-9182',
    secondsRemaining: 180,
    failedVerificationAttempts: 0,
    isLockedOut: false,
    telemetry: {
      batteryPercentage: 92,
      isCharging: true,
      networkType: 'WiFi',
      pingLatencyMs: 45,
      lastHeartbeatUtc: '2026-11-03T18:00:00Z'
    },
    createdAtUtc: '2026-11-03T17:00:00Z',
    lastActiveAtUtc: '2026-11-03T18:30:00Z',
    reconnectAttempts: 0
  };

  beforeEach(() => {
    vi.useFakeTimers();

    httpMock = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/sessions')) return of({ sessions: [mockSession] });
        if (url.includes('/status')) return of({ packetLogs: [] });
        return of({});
      }),
      post: vi.fn().mockReturnValue(of({ pairingCode: '9182-4829', sessionId: 'sess-new-202', ttlSecondsRemaining: 180 })),
      delete: vi.fn().mockReturnValue(of({ success: true }))
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpMock }
    ]);

    injector.runInContext(() => {
      component = new WhatsAppCompanionStudioComponent();
    });
  });

  afterEach(() => {
    component.ngOnDestroy();
    vi.useRealTimers();
  });

  it('should initialize with default phone number and device model', () => {
    expect(component).toBeTruthy();
    expect(component.phoneNumber()).toContain('+216');
    expect(component.deviceModel()).toContain('Multi-Device');
    expect(component.isGenerating()).toBe(false);
  });

  it('should load active companion fleet sessions', () => {
    component.loadSessions();

    expect(httpMock.get).toHaveBeenCalled();
    expect(component.sessions().length).toBe(1);
    expect(component.sessions()[0].phoneNumber).toBe('+216 71 998 801');
    expect(component.sessions()[0].telemetry.batteryPercentage).toBe(92);
  });

  it('should request and generate pairing code for device connection', () => {
    component.requestPairingCode();

    expect(httpMock.post).toHaveBeenCalled();
    expect(component.currentPairingCode()).toBe('9182-4829');
    expect(component.operationSuccess()).toBe(true);
  });

  it('should select active companion session for telemetry inspection', () => {
    component.selectSession(mockSession);

    expect(component.activeSession()).toEqual(mockSession);
    expect(component.activeSession()?.state).toBe('Connected');
  });
});
