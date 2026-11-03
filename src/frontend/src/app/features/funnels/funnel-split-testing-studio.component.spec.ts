// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { FunnelSplitTestingStudioComponent, FunnelStepOutcome } from './funnel-split-testing-studio.component';

describe('FunnelSplitTestingStudioComponent', () => {
  let component: FunnelSplitTestingStudioComponent;
  let httpClientMock: any;

  const mockSummaries = [
    {
      funnelId: 'funnel-saas-enterprise',
      funnelName: 'Enterprise SaaS Funnel',
      description: 'Main enterprise funnel',
      currency: 'USD',
      totalVisitors: 500,
      totalRevenue: 25000,
      variantCount: 2,
      autoCannibalizationActive: true,
      winningVariantId: 'B',
      lastActivityAt: '2026-11-03T20:00:00Z'
    }
  ];

  const mockExperiment = {
    funnelId: 'funnel-saas-enterprise',
    funnelName: 'Enterprise SaaS Funnel',
    description: 'Main enterprise funnel',
    currency: 'USD',
    outcomeValues: { bounce: 0, leadCaptured: 10, checkoutInitiated: 25, orderPaid: 100, upsellAccepted: 50 },
    explorationFloor: 0.05,
    minVisitorThreshold: 100,
    superiorityThreshold: 0.95,
    autoCannibalizationActive: true,
    winningVariantId: 'B',
    createdAt: '2026-10-01T00:00:00Z',
    lastActivityAt: '2026-11-03T20:00:00Z',
    variants: [
      {
        variantId: 'A',
        variantName: 'Variant A (Control)',
        description: 'Baseline version',
        isBaseline: true,
        isWinner: false,
        isCannibalized: true,
        alphas: [100, 30, 20, 10, 5],
        initialAlphas: [1, 1, 1, 1, 1],
        totalVisitors: 200,
        totalRevenue: 8000,
        winProbability: 0.1,
        superiorityVsBaseline: 0.0,
        expectedRpv: 40.0,
        credibleIntervalLow: 30.0,
        credibleIntervalHigh: 50.0,
        paidConversionRate: 0.05,
        leadConversionRate: 0.15,
        recommendedTrafficShare: 0.05
      },
      {
        variantId: 'B',
        variantName: 'Variant B (Urgency Hook)',
        description: 'Challenger version',
        isBaseline: false,
        isWinner: true,
        isCannibalized: false,
        alphas: [80, 50, 40, 30, 15],
        initialAlphas: [1, 1, 1, 1, 1],
        totalVisitors: 300,
        totalRevenue: 17000,
        winProbability: 0.9,
        superiorityVsBaseline: 0.98,
        expectedRpv: 56.67,
        credibleIntervalLow: 45.0,
        credibleIntervalHigh: 68.0,
        paidConversionRate: 0.10,
        leadConversionRate: 0.25,
        recommendedTrafficShare: 0.95
      }
    ]
  };

  beforeEach(() => {
    httpClientMock = {
      get: vi.fn((url: string) => {
        if (url === '/api/v1/funnels/split-testing') {
          return of(mockSummaries);
        }
        if (url.startsWith('/api/v1/funnels/split-testing/')) {
          return of(mockExperiment);
        }
        return of({});
      }),
      post: vi.fn((url: string, body: any) => {
        if (url.includes('/route-visitor')) {
          return of({
            funnelId: 'funnel-saas-enterprise',
            visitorId: 'vis-404',
            selectedVariantId: 'B',
            selectedVariantName: 'Variant B (Urgency Hook)',
            routingReason: 'Thompson Sampling (Highest Sampled RPV)',
            sampledRpvs: { A: 38.2, B: 59.4 },
            explorationRate: 0.05,
            timestamp: '2026-11-03T20:15:00Z'
          });
        }
        if (url.includes('/record-outcome')) {
          return of({
            funnelId: 'funnel-saas-enterprise',
            variantId: 'B',
            outcome: FunnelStepOutcome.OrderPaid,
            attributedValue: 100,
            totalVisitors: 301,
            updatedAlphas: [80, 50, 40, 31, 15],
            expectedRpv: 56.8,
            paidConversionRate: 0.103,
            leadConversionRate: 0.25,
            autoCannibalizationActive: true,
            winningVariantId: 'B',
            statusMessage: 'OrderPaid outcome attributed to Variant B (+100 USD).'
          });
        }
        return of({});
      })
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpClientMock }
    ]);

    injector.runInContext(() => {
      component = new FunnelSplitTestingStudioComponent();
    });
  });

  it('should initialize and load experiment summaries and details', () => {
    component.ngOnInit();
    expect(component).toBeTruthy();
    expect(httpClientMock.get).toHaveBeenCalledWith('/api/v1/funnels/split-testing');
    expect(component.experimentSummaries().length).toBe(1);
    expect(component.currentExperiment()?.funnelName).toBe('Enterprise SaaS Funnel');
  });

  it('should compute aggregate metrics and best variant correctly', () => {
    component.ngOnInit();
    expect(component.totalExperimentVisitors()).toBe(500);
    expect(component.totalExperimentRevenue()).toBe(25000);
    expect(component.bestVariantRpv()).toBeCloseTo(56.67, 1);
    expect(component.bestVariantId()).toBe('B');
  });

  it('should route a single visitor via Thompson sampling', () => {
    component.routeSingleVisitor();

    expect(httpClientMock.post).toHaveBeenCalledWith(
      '/api/v1/funnels/split-testing/route-visitor',
      { funnelId: 'funnel-saas-enterprise' }
    );
    expect(component.lastRouteResult()?.selectedVariantId).toBe('B');
    expect(component.actionStatusMessage()).toContain('routed to Variant B');
  });

  it('should record manual outcome for a variant', () => {
    component.recordManualOutcome('B', FunnelStepOutcome.OrderPaid);

    expect(httpClientMock.post).toHaveBeenCalledWith(
      '/api/v1/funnels/split-testing/record-outcome',
      expect.objectContaining({
        funnelId: 'funnel-saas-enterprise',
        variantId: 'B',
        outcome: FunnelStepOutcome.OrderPaid
      })
    );
    expect(component.actionStatusMessage()).toContain('OrderPaid outcome attributed to Variant B');
  });

  it('should switch selected funnel and reload details', () => {
    component.onSelectFunnel('funnel-saas-enterprise');

    expect(component.selectedFunnelId()).toBe('funnel-saas-enterprise');
    expect(component.lastRouteResult()).toBeNull();
    expect(component.actionStatusMessage()).toBe('');
  });
});
