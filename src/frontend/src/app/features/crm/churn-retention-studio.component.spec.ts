// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { ChurnRetentionStudioComponent, CustomerRfmProfile } from './churn-retention-studio.component';

describe('ChurnRetentionStudioComponent (RFM Segmentation & Churn Prevention Studio)', () => {
  let component: ChurnRetentionStudioComponent;
  let mockHttp: any;

  const mockCustomer: CustomerRfmProfile = {
    customerId: 'cust-101',
    tenantId: 'tenant-1',
    customerName: 'Karim Jaziri',
    customerEmail: 'karim@jazirigroup.tn',
    customerPhone: '+216 98 123 456',
    company: 'Jaziri Retail Group',
    recencyDays: 65,
    frequencyOrders: 8,
    monetaryTotalLtv: 14500,
    recencyScore: 2,
    frequencyScore: 4,
    monetaryScore: 5,
    rfmCell: '245',
    segment: 'AtRisk',
    unresolvedTickets: 3,
    negativeCsatCount: 1,
    churnProbability: 0.78,
    riskTier: 'Critical',
    lastEvaluatedAtUtc: new Date().toISOString(),
    lastPurchaseDateUtc: new Date(Date.now() - 65 * 86400000).toISOString(),
    activePlaybook: null
  };

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/overview-kpis')) {
          return of({
            totalMonitoredAccounts: 120,
            atRiskLtvAtStake: 450000,
            activeWinbackCampaigns: 14,
            recoveredRevenue30d: 82000,
            avgNetRetentionRate: 108.5
          });
        }
        if (url.includes('/rfm-cohorts')) {
          return of([
            { segment: 'Champions', segmentLabel: 'Champions', customerCount: 40, percentage: 33.3, totalLtv: 500000 },
            { segment: 'AtRisk', segmentLabel: 'At Risk', customerCount: 15, percentage: 12.5, totalLtv: 200000 }
          ]);
        }
        if (url.includes('/at-risk-customers')) {
          return of([mockCustomer]);
        }
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/trigger-winback')) {
          return of({
            id: 'winback-1',
            tenantId: 'tenant-1',
            customerId: body?.customerId,
            customerName: 'Karim Jaziri',
            customerEmail: 'karim@jazirigroup.tn',
            playbookType: body?.playbookType || 'VipDiscountOffer',
            channel: body?.channel || 'WhatsApp',
            status: 'Dispatched',
            offerDetails: body?.offerDetails || '20% off reorder',
            dispatchedAtUtc: new Date().toISOString(),
            expiresAtUtc: new Date(Date.now() + 14 * 86400000).toISOString()
          });
        }
        if (url.includes('/record-conversion')) {
          return of({
            success: true,
            recoveredRevenue: body?.orderAmount || 2500
          });
        }
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new ChurnRetentionStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with retention KPIs, cohorts, and at-risk customers', () => {
    expect(component).toBeTruthy();
    expect(component.kpis()).not.toBeNull();
    expect(component.kpis()?.totalMonitoredAccounts).toBe(120);
    expect(component.atRiskCustomers().length).toBe(1);
    expect(component.criticalCount()).toBe(1);
  });

  it('should compute simulated churn probability, risk tier, and playbook recommendations', () => {
    component.simRecencyDays.set(90);
    component.simFrequencyOrders.set(2);
    component.simUnresolvedTickets.set(3);
    component.simNegativeCsat.set(2);

    expect(component.simChurnProbability()).toBeGreaterThan(0.5);
    expect(component.simRiskTier()).toBe('Critical');
    expect(component.simRecommendedPlaybook()).toContain('ExecutivePulseSurvey');
  });

  it('should filter at-risk customers by tier and segment', () => {
    expect(component.filteredCustomers().length).toBe(1);

    component.selectedTierFilter.set('Low');
    expect(component.filteredCustomers().length).toBe(0);

    component.selectedTierFilter.set('Critical');
    expect(component.filteredCustomers().length).toBe(1);
  });

  it('should open trigger modal, dispatch winback playbook, and close drawer', () => {
    component.openTriggerWinbackModal(mockCustomer);
    expect(component.activeDrawer()).toBe('triggerWinback');
    expect(component.selectedCustomerForWinback()).toBe(mockCustomer);

    component.submitTriggerWinback();
    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.activeDrawer()).toBeNull();
  });
});
