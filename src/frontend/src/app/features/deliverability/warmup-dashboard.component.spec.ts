// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { of } from 'rxjs';
import { WarmupDashboardComponent } from './warmup-dashboard.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { WarmupPeerPairDto, SpamRescueSummaryDto, DnsDeliverabilityReportDto } from '../../core/models/inspiration.models';

describe('WarmupDashboardComponent', () => {
  let component: WarmupDashboardComponent;
  let apiServiceMock: any;
  let authServiceMock: any;

  const mockPeers: WarmupPeerPairDto[] = [
    {
      senderAccountId: 'acc-1',
      senderEmail: 'sales@sgrowing.io',
      peerEmail: 'outreach@peer-mesh.org',
      dailyWarmupQuota: 25,
      currentSentToday: 15,
      currentReceivedToday: 14,
      isActive: true
    }
  ];

  const mockRescueStats: SpamRescueSummaryDto = {
    totalScanned: 60,
    rescuedFromSpam: 8,
    markedImportant: 12,
    movedToInbox: 8,
    lastRunUtc: '2026-11-03T18:00:00Z'
  };

  const mockDnsReport: DnsDeliverabilityReportDto = {
    domain: 'sgrowing.io',
    hasSpfRecord: true,
    spfRecordValue: 'v=spf1 include:_spf.google.com ~all',
    hasDkimRecord: true,
    dkimKeyLengthBits: 2048,
    hasDmarcRecord: true,
    dmarcPolicy: 'p=reject',
    hasBimiRecord: true,
    hasMxRecord: true,
    deliverabilityHealthScore: 99
  };

  beforeEach(() => {
    apiServiceMock = {
      pairWarmupPeers: vi.fn().mockReturnValue(of(mockPeers)),
      runSpamRescueSweep: vi.fn().mockReturnValue(of(mockRescueStats)),
      getDnsDiagnostics: vi.fn().mockReturnValue(of(mockDnsReport))
    };

    authServiceMock = {
      currentTenant: signal({ id: 't-1', name: 'Demo Tenant' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new WarmupDashboardComponent();
    });
  });

  it('should initialize and load peer pairs and DNS diagnostics on init', () => {
    component.ngOnInit();

    expect(component).toBeTruthy();
    expect(apiServiceMock.pairWarmupPeers).toHaveBeenCalledWith('t-1');
    expect(apiServiceMock.getDnsDiagnostics).toHaveBeenCalledWith('sgrowing.io');
    expect(component.peerPairs().length).toBe(1);
    expect(component.dnsReport()?.deliverabilityHealthScore).toBe(99);
  });

  it('should execute spam rescue sweep', () => {
    component.runRescueSweep();

    expect(apiServiceMock.runSpamRescueSweep).toHaveBeenCalledWith('t-1');
    expect(component.rescueStats()?.rescuedFromSpam).toBe(8);
    expect(component.isRescuing()).toBe(false);
  });

  it('should trigger peer pairing on demand', () => {
    component.pairPeers();

    expect(apiServiceMock.pairWarmupPeers).toHaveBeenCalledWith('t-1');
    expect(component.peerPairs().length).toBe(1);
    expect(component.isPairing()).toBe(false);
  });

  it('should run DNS diagnostic inspection for custom domain', () => {
    component.inspectDomain = 'cold-mail.io';
    component.diagnoseDns();

    expect(apiServiceMock.getDnsDiagnostics).toHaveBeenCalledWith('cold-mail.io');
    expect(component.isDiagnosing()).toBe(false);
  });
});
