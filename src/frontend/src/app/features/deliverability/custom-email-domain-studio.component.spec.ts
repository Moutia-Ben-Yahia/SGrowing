// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { CustomEmailDomainStudioComponent } from './custom-email-domain-studio.component';
import { CustomDomainSummaryDto, CustomEmailDomainDetailDto, DeliverabilityDomainMetricsDto } from '../../core/models/deliverability.models';

describe('CustomEmailDomainStudioComponent', () => {
  let component: CustomEmailDomainStudioComponent;
  let httpClientMock: any;

  const mockDomainSummary: CustomDomainSummaryDto = {
    id: 'dom-001',
    domainName: 'outreach.sgrowing.io',
    returnPathDomain: 'mail.outreach.sgrowing.io',
    dmarcPolicy: 'quarantine',
    status: 'Active',
    healthScore: 98,
    isDkimAligned: true,
    isSpfAligned: true,
    isDmarcAligned: true,
    lastVerifiedAt: '2026-11-03T18:00:00Z',
    createdAt: '2026-10-01T12:00:00Z',
    activeSenderInboxesCount: 8
  };

  const mockDomainDetail: any = {
    id: 'dom-001',
    domain: 'sales.sgrowing.io',
    domainName: 'outreach.sgrowing.io',
    returnPathDomain: 'mail.outreach.sgrowing.io',
    dmarcPolicy: 'quarantine',
    ruaReportEmail: 'dmarc-reports@sgrowing.io',
    status: 'Active',
    healthScore: 98,
    isDkimAligned: true,
    isSpfAligned: true,
    isDmarcAligned: true,
    dnsRecords: [
      {
        id: 'rec-1',
        recordType: 'TXT',
        host: 'outreach.sgrowing.io',
        expectedValue: 'v=spf1 include:_spf.google.com ~all',
        actualValue: 'v=spf1 include:_spf.google.com ~all',
        status: 'Valid',
        purpose: 'SPF Alignment',
        isRequired: true
      }
    ],
    dkimSelector: 'sgrowing2026',
    dkimPublicKey: 'MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...',
    lastVerifiedAt: '2026-11-03T18:00:00Z',
    createdAt: '2026-10-01T12:00:00Z',
    activeSenderInboxesCount: 8
  };

  const mockMetrics: DeliverabilityDomainMetricsDto = {
    totalDomains: 3,
    activeDomains: 2,
    pendingDnsDomains: 1,
    degradedDomains: 0,
    averageHealthScore: 96,
    totalSenderInboxes: 24
  };

  beforeEach(() => {
    httpClientMock = {
      get: vi.fn((url: string) => {
        if (url === '/api/v1/deliverability/custom-domains') {
          return of([mockDomainSummary]);
        }
        if (url === '/api/v1/deliverability/custom-domains/metrics') {
          return of(mockMetrics);
        }
        if (url.startsWith('/api/v1/deliverability/custom-domains/dom-001')) {
          return of(mockDomainDetail);
        }
        return of({});
      }),
      post: vi.fn((url: string, body: any) => {
        if (url.includes('/verify/')) {
          return of({
            domainId: 'dom-001',
            domainName: 'outreach.sgrowing.io',
            isFullyAligned: true,
            healthScore: 100,
            verifiedRecordsCount: 4,
            failedRecordsCount: 0,
            status: 'Active',
            issues: []
          });
        }
        if (url.includes('/custom-domains/register')) {
          return of(mockDomainDetail);
        }
        return of({});
      })
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpClientMock }
    ]);

    injector.runInContext(() => {
      component = new CustomEmailDomainStudioComponent();
    });
  });

  it('should initialize and load custom domains and metrics', () => {
    component.ngOnInit();

    expect(component).toBeTruthy();
    expect(httpClientMock.get).toHaveBeenCalledWith('/api/v1/deliverability/custom-domains');
    expect(component.domains().length).toBe(1);
    expect(component.selectedDomainId()).toBe('dom-001');
    expect(component.activeCount()).toBe(1);
    expect(component.averageHealth()).toBe(98);
  });

  it('should select domain and load domain details', () => {
    component.onDomainSelected('dom-001');

    expect(component.selectedDomainId()).toBe('dom-001');
    expect(httpClientMock.get).toHaveBeenCalledWith('/api/v1/deliverability/custom-domains/dom-001');
    expect(component.selectedDomain()?.domainName).toBe('outreach.sgrowing.io');
  });

  it('should open and close connect domain modal', () => {
    expect(component.showConnectModal()).toBe(false);

    component.openConnectModal();
    expect(component.showConnectModal()).toBe(true);

    component.closeConnectModal();
    expect(component.showConnectModal()).toBe(false);
  });

  it('should submit connect domain and refresh domain list', () => {
    component.newDomainName = 'sales.sgrowing.io';
    component.submitConnectDomain();

    expect(httpClientMock.post).toHaveBeenCalledWith(
      '/api/v1/deliverability/custom-domains/register',
      expect.objectContaining({
        domain: 'sales.sgrowing.io',
        dmarcPolicy: 'quarantine'
      })
    );
    expect(component.showConnectModal()).toBe(false);
    expect(component.notificationMessage()).toContain('Domain sales.sgrowing.io registered');
  });

  it('should verify domain records', () => {
    component.selectedDomainId.set('dom-001');
    component.verifyDomain(true);

    expect(httpClientMock.post).toHaveBeenCalledWith(
      '/api/v1/deliverability/custom-domains/verify/dom-001?passAll=true',
      {}
    );
    expect(component.isVerifying()).toBe(false);
  });
});
