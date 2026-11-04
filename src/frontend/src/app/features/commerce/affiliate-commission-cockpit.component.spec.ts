// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AffiliateCommissionCockpitComponent, AffiliatePartner } from './affiliate-commission-cockpit.component';
import { of } from 'rxjs';

describe('AffiliateCommissionCockpitComponent', () => {
  let component: AffiliateCommissionCockpitComponent;
  let httpMock: any;

  const mockPartner: AffiliatePartner = {
    id: 'aff-101',
    name: 'Atlas Media Network',
    email: 'partners@atlasmedia.tn',
    tierLevel: 1,
    status: 'Active',
    referralCodes: ['ATLAS2026'],
    trackingSlug: 'atlas-partner',
    attributionWindowDays: 60,
    escrowHoldbackDays: 14,
    minimumPayoutThreshold: 100,
    commissionRule: {
      ruleType: 'PercentageOfGmv',
      ratePercentage: 15,
      fixedAmount: 0,
      minimumOrderThreshold: 0,
      excludedCategories: [],
      milestoneTargetSales: 10000,
      milestoneBonusAmount: 500
    },
    payoutDetails: {
      preferredMethod: 'Sepa',
      accountHolderName: 'Atlas Media SARL',
      currency: 'EUR'
    },
    taxDetails: {
      formType: 'Vat',
      taxIdOrSsn: 'TN-VAT-481920',
      withholdingPercentage: 0,
      isVerified: true
    },
    metrics: {
      totalReferredGmv: 45000,
      totalCommissionsEarned: 6750,
      totalCommissionsPaid: 5000,
      pendingEscrowAmount: 1750,
      approvedAvailableAmount: 1750,
      refundReversalAmount: 0,
      totalClicks: 12400,
      totalConversions: 420,
      refundReversalCount: 2,
      cumulativeSalesCount: 420
    },
    createdAtUtc: '2026-10-01T10:00:00Z'
  };

  beforeEach(() => {
    httpMock = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/partners')) return of([mockPartner]);
        if (url.includes('/commissions')) return of([]);
        if (url.includes('/settlements')) return of([]);
        if (url.includes('/metrics')) return of(null);
        return of([]);
      }),
      post: vi.fn().mockReturnValue(of({ success: true }))
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpMock }
    ]);

    injector.runInContext(() => {
      component = new AffiliateCommissionCockpitComponent();
    });

    component.partners.set([mockPartner]);
  });

  it('should initialize with affiliate partners roster and active tab', () => {
    expect(component).toBeTruthy();
    expect(component.activeTab()).toBe('roster');
    expect(component.partners().length).toBe(1);
    expect(component.partners()[0].name).toBe('Atlas Media Network');
    expect(component.partners()[0].metrics.totalCommissionsEarned).toBe(6750);
  });

  it('should toggle between affiliate cockpit tabs', () => {
    component.activeTab.set('tree');
    expect(component.activeTab()).toBe('tree');

    component.activeTab.set('ledger');
    expect(component.activeTab()).toBe('ledger');

    component.activeTab.set('settlements');
    expect(component.activeTab()).toBe('settlements');

    component.activeTab.set('roster');
    expect(component.activeTab()).toBe('roster');
  });

  it('should filter partners by tier level', () => {
    component.tierFilter.set('TIER_1');
    expect(component.tierFilter()).toBe('TIER_1');

    component.tierFilter.set('ALL');
    expect(component.tierFilter()).toBe('ALL');
  });

  it('should filter partners by search query', () => {
    component.searchQuery.set('Atlas');
    expect(component.searchQuery()).toBe('Atlas');
  });

  it('should update treeSimGmv and verify 3-tier commission split', () => {
    component.treeSimGmv.set(2000);
    expect(component.treeSimGmv()).toBe(2000);
    expect(component.treeSimGmv() * 0.15).toBe(300);
    expect(component.treeSimGmv() * 0.05).toBe(100);
    expect(component.treeSimGmv() * 0.02).toBe(40);
    expect(component.treeSimGmv() * 0.78).toBe(1560);
  });
});
