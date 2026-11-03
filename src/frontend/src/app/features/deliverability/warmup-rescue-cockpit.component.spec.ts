// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { of } from 'rxjs';
import { WarmupRescueCockpitComponent } from './warmup-rescue-cockpit.component';
import { ApiService } from '../../core/services/api.service';
import { EnrolledWarmupMailboxDto, SpamRescueLogEntry, DeliverabilityNetworkMetrics, DnsAndBlacklistAuditResult } from '../../core/models/deliverability.models';

describe('WarmupRescueCockpitComponent', () => {
  let component: WarmupRescueCockpitComponent;
  let apiServiceMock: any;

  const mockMailbox: EnrolledWarmupMailboxDto = {
    id: 'mb-01',
    email: 'outreach@sgrowing.io',
    provider: 'GoogleWorkspace',
    status: 'Ramping',
    healthScore: 96,
    dailyRampLimit: 40,
    currentDayRamp: 12,
    warmupVolumeSent: 120,
    warmupVolumeReceived: 118,
    rescuedFromSpamCount: 14,
    spamRatePercent: 0.8,
    spfStatus: 'Valid',
    dkimStatus: 'Valid',
    dmarcStatus: 'Valid',
    lastActiveAt: '2026-11-03T18:00:00Z'
  };

  const mockRescueLog: SpamRescueLogEntry = {
    id: 'log-1',
    mailboxEmail: 'outreach@sgrowing.io',
    subject: 'Follow up on partnership',
    senderEmail: 'peer@swarm-network.org',
    originalFolder: 'Spam',
    rescuedToFolder: 'Inbox',
    rescuedAt: '2026-11-03T18:00:00Z',
    markedImportant: true,
    espProvider: 'GoogleWorkspace'
  };

  const mockMetrics: DeliverabilityNetworkMetrics = {
    totalEnrolledMailboxes: 1,
    activeMailboxes: 1,
    rampingMailboxes: 1,
    optimalMailboxes: 0,
    degradedMailboxes: 0,
    totalDailyCapacity: 40,
    capacityUtilizedToday: 12,
    totalWarmupExchanges24h: 24,
    totalSpamRescues24h: 14,
    averageHealthScore: 96,
    networkSpamRescueEfficiencyRate: 98.2
  };

  const mockAudit: DnsAndBlacklistAuditResult = {
    domain: 'sgrowing.io',
    auditedAt: '2026-11-03T18:00:00Z',
    overallHealthScore: {
      score: 96,
      grade: 'A+',
      status: 'Optimal',
      summary: 'All DNS authentication and blacklist records clean.'
    },
    dnsRecords: [],
    blacklists: [
      {
        rblName: 'Spamhaus SBL',
        rblHost: 'sbl.spamhaus.org',
        isListed: false,
        responseCode: null,
        description: 'Spamhaus Block List'
      }
    ]
  };

  beforeEach(() => {
    apiServiceMock = {
      getWarmupMailboxes: vi.fn().mockReturnValue(of([mockMailbox])),
      getSpamRescueLogs: vi.fn().mockReturnValue(of([mockRescueLog])),
      getWarmupMetrics: vi.fn().mockReturnValue(of(mockMetrics)),
      auditDomainDns: vi.fn().mockReturnValue(of(mockAudit)),
      executeSpamRescueScan: vi.fn().mockReturnValue(of({
        rescuedMessagesCount: 5,
        achievedReplyRatio: 0.25
      })),
      enrollWarmupMailbox: vi.fn().mockReturnValue(of(mockMailbox))
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock }
    ]);

    injector.runInContext(() => {
      component = new WarmupRescueCockpitComponent();
    });
  });

  it('should initialize and load mailboxes, logs, and metrics on init', () => {
    component.ngOnInit();

    expect(component).toBeTruthy();
    expect(apiServiceMock.getWarmupMailboxes).toHaveBeenCalled();
    expect(apiServiceMock.getSpamRescueLogs).toHaveBeenCalledWith(50);
    expect(apiServiceMock.getWarmupMetrics).toHaveBeenCalled();
    expect(component.mailboxes().length).toBe(1);
    expect(component.rescueLogs().length).toBe(1);
    expect(component.overallHealthScore()).toBe(96);
    expect(component.healthGrade()).toBe('A+');
  });

  it('should switch between tabs', () => {
    component.activeTab.set('rescue');
    expect(component.activeTab()).toBe('rescue');

    component.activeTab.set('dnsbl');
    expect(component.activeTab()).toBe('dnsbl');

    component.activeTab.set('exchange');
    expect(component.activeTab()).toBe('exchange');
  });

  it('should trigger spam rescue sweep', () => {
    component.triggerSpamRescueSweep();

    expect(apiServiceMock.executeSpamRescueScan).toHaveBeenCalled();
    expect(component.isRescuing()).toBe(false);
    expect(component.notificationMessage()).toContain('Autonomous Spam Rescue Sweep completed');
  });

  it('should filter blacklists correctly', () => {
    component.auditResult.set(mockAudit);

    component.blacklistFilter.set('all');
    expect(component.filteredBlacklists().length).toBe(1);

    component.blacklistFilter.set('listed');
    expect(component.filteredBlacklists().length).toBe(0);
  });

  it('should inspect DNS domain and update search domain', () => {
    component.inspectDnsDomain('my-brand.com');

    expect(component.searchDomain).toBe('my-brand.com');
    expect(component.activeTab()).toBe('dnsbl');
    expect(apiServiceMock.auditDomainDns).toHaveBeenCalledWith('my-brand.com');
  });
});
