// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { DeliverabilityStudioComponent } from './deliverability-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { of } from 'rxjs';

describe('DeliverabilityStudioComponent', () => {
  let component: DeliverabilityStudioComponent;
  let apiServiceMock: any;
  let authServiceMock: any;

  const mockDnsRecord = {
    overallHealthScore: 98,
    spfRecord: 'v=spf1 include:_spf.sgrowing.io ~all',
    dkimSelector: 'sgrowing2026',
    dmarcPolicy: 'reject'
  };

  const mockSpamReport = {
    spamScore: 12,
    verdict: 'Safe'
  };

  const mockWarmup = {
    id: 'warmup-101',
    currentDayNumber: 3,
    totalSimulatedSent: 45,
    totalSimulatedReceived: 36,
    spamRescueCount: 3
  };

  const mockLogs = [
    {
      id: 'log-1',
      timestampUtc: '2026-11-03T18:00:00Z',
      peerInboxEmail: 'warmup-node@warmbly.net',
      direction: 'Sent' as const
    }
  ];

  beforeEach(() => {
    apiServiceMock = {
      checkDomainDns: vi.fn().mockReturnValue(of(mockDnsRecord)),
      analyzeSpamRisk: vi.fn().mockReturnValue(of(mockSpamReport)),
      startWarmup: vi.fn().mockReturnValue(of(mockWarmup)),
      getWarmupLogs: vi.fn().mockReturnValue(of(mockLogs)),
      simulateWarmupStep: vi.fn().mockReturnValue(of({ ...mockWarmup, spamRescueCount: 4 }))
    };

    authServiceMock = {
      currentTenant: signal({ id: 'tenant-test-123', name: 'Test Tenant' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new DeliverabilityStudioComponent();
    });

    component.ngOnInit();
  });

  it('should initialize DNS health, spam analysis, and peer warmup cadence on startup', () => {
    expect(component).toBeTruthy();
    expect(component.activeTab()).toBe('dns');
    expect(apiServiceMock.checkDomainDns).toHaveBeenCalledWith({
      tenantId: 'tenant-test-123',
      domainName: 'mail.sgrowing.io'
    });
    expect(component.dnsRecord()?.overallHealthScore).toBe(98);
    expect(component.spamReport()?.verdict).toBe('Safe');
    expect(component.warmup()?.id).toBe('warmup-101');
    expect(component.warmupLogs().length).toBe(1);
  });

  it('should toggle active studio tabs correctly', () => {
    component.activeTab.set('spam');
    expect(component.activeTab()).toBe('spam');

    component.activeTab.set('warmup');
    expect(component.activeTab()).toBe('warmup');

    component.activeTab.set('dns');
    expect(component.activeTab()).toBe('dns');
  });

  it('should trigger spam risk heuristic check when inputs are evaluated', () => {
    component.spamSubject = 'Subject with high urgency $$$';
    component.spamBody = 'Earn money quickly by clicking here!';
    component.analyzeSpam();

    expect(apiServiceMock.analyzeSpamRisk).toHaveBeenCalledWith({
      tenantId: 'tenant-test-123',
      subject: 'Subject with high urgency $$$',
      bodyHtml: 'Earn money quickly by clicking here!'
    });
    expect(component.spamReport()).toEqual(mockSpamReport);
  });

  it('should simulate peer warmup step and refresh exchange telemetry', () => {
    component.simulateStep();

    expect(apiServiceMock.simulateWarmupStep).toHaveBeenCalledWith('warmup-101', 'tenant-test-123');
    expect(component.warmup()?.spamRescueCount).toBe(4);
    expect(apiServiceMock.getWarmupLogs).toHaveBeenCalledWith('warmup-101', 'tenant-test-123');
  });

  it('should fallback to default tenant ID when auth tenant is null and handle null warmup gracefully', () => {
    authServiceMock.currentTenant.set(null);
    expect(component.tenantId).toBe('00000000-0000-0000-0000-000000000001');

    component.checkDns();
    expect(apiServiceMock.checkDomainDns).toHaveBeenCalledWith({
      tenantId: '00000000-0000-0000-0000-000000000001',
      domainName: 'mail.sgrowing.io'
    });

    component.warmup.set(null);
    expect(() => component.simulateStep()).not.toThrow();
  });
});
