// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import {
  ProductAnalyticsCockpitComponent,
  ActiveUsersMetrics,
  CohortRetentionMatrix,
  CrashClustersResponse,
  FunnelEvaluationResult,
  TelemetryEventItem
} from './product-analytics-cockpit.component';

describe('ProductAnalyticsCockpitComponent', () => {
  let component: ProductAnalyticsCockpitComponent;
  let mockHttp: any;

  const mockActiveMetrics: ActiveUsersMetrics = {
    dau: 120,
    wau: 450,
    mau: 1200,
    stickinessRatioPercent: 37.5,
    stickinessBenchmarkHealth: 'Good',
    todayEventsCount: 2400,
    totalEventsCount: 65000,
    dauDayOverDayGrowthPercent: 5.2,
    wauWeekOverWeekGrowthPercent: 3.1,
    timeSeries: []
  };

  const mockCohortMatrix: CohortRetentionMatrix = {
    granularity: 'weekly',
    retentionIntervals: [0, 1, 7, 14, 30],
    averageRetentionCurve: { 0: 100, 1: 65, 7: 42, 14: 31, 30: 22 },
    cohorts: [
      {
        cohortId: '2026-W40',
        cohortLabel: 'Week 40',
        cohortStartDate: '2026-10-01',
        totalAcquiredUsers: 100,
        retentionPercentages: { 0: 100, 1: 68, 7: 45, 14: 34, 30: 25 },
        retainedUserCounts: { 0: 100, 1: 68, 7: 45, 14: 34, 30: 25 }
      }
    ]
  };

  const mockCrashResponse: CrashClustersResponse = {
    totalCrashOccurrences: 5,
    totalUniqueAffectedUsers: 3,
    totalClustersCount: 1,
    clusters: [
      {
        signature: 'NullReferenceException:OrderProcessor.cs:42',
        exceptionType: 'NullReferenceException',
        normalizedErrorMessage: 'Object reference not set to an instance of an object',
        stackTraceSample: 'at SGrowing.Api.OrderProcessor.Process() in OrderProcessor.cs:line 42',
        totalOccurrences: 5,
        uniqueUsersAffected: 3,
        affectedVersions: ['1.0.4'],
        platformBreakdown: { Web: 5 },
        recentBreadcrumbs: ['Click button [Confirm]', 'Navigate to /checkout'],
        firstSeenUtc: '2026-11-01T10:00:00Z',
        lastSeenUtc: '2026-11-03T11:00:00Z',
        severity: 'Critical',
        status: 'Open'
      }
    ]
  };

  const mockFunnelResult: FunnelEvaluationResult = {
    funnelName: 'Storefront COD Checkout Funnel',
    evaluatedAtUtc: '2026-11-03T10:00:00Z',
    totalEnteredUsers: 1000,
    totalCompletedUsers: 420,
    overallConversionPercent: 42.0,
    biggestDropoffStep: 'Address Form',
    biggestDropoffPercent: 28.5,
    steps: [
      {
        stepIndex: 1,
        stepName: 'Product View',
        reachedCount: 1000,
        droppedCount: 300,
        stepConversionPercent: 70.0,
        overallConversionPercent: 100.0,
        dropoffPercent: 30.0,
        averageDurationSecondsFromPrevious: 0
      },
      {
        stepIndex: 2,
        stepName: 'Order Confirmed',
        reachedCount: 420,
        droppedCount: 0,
        stepConversionPercent: 60.0,
        overallConversionPercent: 42.0,
        dropoffPercent: 0,
        averageDurationSecondsFromPrevious: 45
      }
    ]
  };

  const mockEvents: TelemetryEventItem[] = [
    {
      eventId: 'evt-1',
      userId: 'usr-42',
      sessionId: 'sess-88',
      eventType: 'click',
      eventName: 'checkout_click',
      timestampUtc: '2026-11-03T11:00:00Z',
      url: '/cart'
    }
  ];

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/active-users')) return of(mockActiveMetrics);
        if (url.includes('/cohorts/retention')) return of(mockCohortMatrix);
        if (url.includes('/crashes/clusters')) return of(mockCrashResponse);
        if (url.includes('/funnels/evaluate')) return of(mockFunnelResult);
        if (url.includes('/events/recent')) return of(mockEvents);
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/funnels/evaluate')) return of(mockFunnelResult);
        return of({ success: true });
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new ProductAnalyticsCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize and load active metrics, cohort retention, crash clusters, and funnels', () => {
    expect(component).toBeTruthy();
    expect(mockHttp.get).toHaveBeenCalled();
    expect(component.activeMetrics()?.dau).toBe(120);
    expect(component.cohortMatrix()?.cohorts.length).toBe(1);
    expect(component.crashResponse()?.totalCrashOccurrences).toBe(5);
    expect(component.funnelResult()?.overallConversionPercent).toBe(42.0);
  });

  it('should change cohort retention granularity and reload retention matrix', () => {
    component.changeGranularity('monthly');
    expect(component.retentionGranularity()).toBe('monthly');
    expect(mockHttp.get).toHaveBeenCalledWith(expect.stringContaining('granularity=monthly'));
  });

  it('should toggle cluster stack trace inspection expansion', () => {
    const sig = mockCrashResponse.clusters[0].signature;
    expect(component.isClusterExpanded(sig)).toBe(false);

    component.toggleClusterExpand(sig);
    expect(component.isClusterExpanded(sig)).toBe(true);

    component.toggleClusterExpand(sig);
    expect(component.isClusterExpanded(sig)).toBe(false);
  });

  it('should switch navigation tabs between overview, retention, funnels, crashes, and activity', () => {
    component.activeTab.set('retention');
    expect(component.activeTab()).toBe('retention');

    component.activeTab.set('funnels');
    expect(component.activeTab()).toBe('funnels');

    component.activeTab.set('crashes');
    expect(component.activeTab()).toBe('crashes');
  });
});
