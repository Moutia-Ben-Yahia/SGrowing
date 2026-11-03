// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { of } from 'rxjs';
import { MultiInboxCockpitComponent } from './multi-inbox-cockpit.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { MultiInboxAccountDto } from '../../core/models/deliverability.models';

describe('MultiInboxCockpitComponent', () => {
  let component: MultiInboxCockpitComponent;
  let apiServiceMock: any;
  let authServiceMock: any;

  const mockInbox: MultiInboxAccountDto = {
    id: 'inbox-1',
    tenantId: 't-1',
    email: 'sarah@growth-agency.com',
    displayName: 'Sarah Growth',
    provider: 'GoogleWorkspace',
    dailyQuota: 50,
    sentTodayCount: 20,
    remainingDailyQuota: 30,
    warmupEnabled: true,
    warmupRampDailyIncrement: 2,
    healthScore: 98,
    isActive: true,
    status: 'Active',
    totalDispatched: 500,
    totalDelivered: 495,
    totalBounced: 5,
    totalReplied: 80,
    totalOpened: 260,
    bounceRate: 1.0,
    replyRate: 16.0,
    openRate: 52.0,
    hasSpf: true,
    hasDkim: true,
    hasDmarc: true,
    domain: 'growth-agency.com',
    createdAtUtc: '2026-10-01T00:00:00Z'
  };

  const mockRadar = {
    totalInboxes: 1,
    activeInboxes: 1,
    warmupInboxes: 0,
    quarantinedInboxes: 0,
    totalDailyCapacity: 50,
    sentTodayTotal: 20,
    remainingCapacityTotal: 30,
    overallHealthIndex: 98,
    radarAxes: [
      { axisName: 'DNS Authentication', score: 98, benchmark: 95 }
    ]
  };

  beforeEach(() => {
    apiServiceMock = {
      getMultiInboxAccounts: vi.fn().mockReturnValue(of([mockInbox])),
      getDeliverabilityRadar: vi.fn().mockReturnValue(of(mockRadar)),
      selectNextRotatingSender: vi.fn().mockReturnValue(of({
        senderInboxId: 'inbox-1',
        senderEmail: 'sarah@growth-agency.com',
        displayName: 'Sarah Growth',
        strategyUsed: 'HealthWeightedRoundRobin'
      })),
      simulateRotationBatch: vi.fn().mockReturnValue(of({
        totalAllocated: 50,
        allocations: [{ inboxEmail: 'sarah@growth-agency.com', count: 50 }]
      })),
      registerSenderInbox: vi.fn().mockReturnValue(of(mockInbox))
    };

    authServiceMock = {
      currentTenant: signal({ id: 't-1', name: 'Demo Tenant' }),
      currentUser: signal({ id: 'u-1', tenantId: 't-1' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new MultiInboxCockpitComponent();
    });
  });

  it('should initialize and load inboxes and radar on init', () => {
    component.ngOnInit();

    expect(component).toBeTruthy();
    expect(apiServiceMock.getMultiInboxAccounts).toHaveBeenCalledWith('t-1');
    expect(apiServiceMock.getDeliverabilityRadar).toHaveBeenCalledWith('t-1');
    expect(component.inboxes().length).toBe(1);
    expect(component.activeInboxesCount()).toBe(1);
    expect(component.avgHealthScore()).toBe(98);
  });

  it('should filter inboxes by query and status', () => {
    component.inboxes.set([mockInbox]);

    component.searchQuery = 'sarah';
    component.inboxes.set([mockInbox]);
    expect(component.filteredInboxes().length).toBe(1);

    component.searchQuery = 'nonexistent';
    component.inboxes.set([mockInbox]);
    expect(component.filteredInboxes().length).toBe(0);
  });

  it('should switch tabs between pool, radar, and simulator', () => {
    component.activeTab.set('radar');
    expect(component.activeTab()).toBe('radar');

    component.activeTab.set('simulator');
    expect(component.activeTab()).toBe('simulator');
  });

  it('should calculate capacity metrics correctly', () => {
    component.inboxes.set([mockInbox]);

    expect(component.totalDailyCapacity()).toBe(50);
    expect(component.totalSentToday()).toBe(20);
    expect(component.totalRemainingCapacity()).toBe(30);
    expect(component.capacityPercent()).toBe(60);
  });

  it('should open and close add sender modal', () => {
    expect(component.showAddModal()).toBe(false);

    component.openAddModal();
    expect(component.showAddModal()).toBe(true);

    component.closeAddModal();
    expect(component.showAddModal()).toBe(false);
  });
});
