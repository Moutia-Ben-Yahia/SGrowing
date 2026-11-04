// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { TelemetryStudioComponent } from './telemetry-studio.component';

describe('TelemetryStudioComponent', () => {
  let component: TelemetryStudioComponent;
  let mockApi: any;
  let mockAuth: any;

  const mockSummary = {
    tenantId: 'tenant-123',
    activeVisitorsCount: 24,
    activeSessionsCount: 38,
    topCountries: [{ countryCode: 'TN', percentage: 65, visitorCount: 16 }],
    topActivePages: [
      { url: 'https://store.sgrowing.io/shop', activeVisitors: 12 }
    ]
  };

  const mockEvents = [
    {
      id: 'evt-1',
      tenantId: 'tenant-123',
      sessionId: 'sess_123',
      visitorId: 'vis_123',
      eventName: 'pageview',
      url: 'https://store.sgrowing.io/shop',
      deviceType: 'Mobile',
      browser: 'Safari Mobile',
      countryCode: 'TN',
      city: 'Tunis',
      timestampUtc: '2026-11-03T10:00:00Z'
    }
  ];

  beforeEach(() => {
    mockApi = {
      getLiveTelemetry: vi.fn().mockReturnValue(of(mockSummary)),
      getRecentTelemetryEvents: vi.fn().mockReturnValue(of(mockEvents)),
      ingestTelemetry: vi.fn().mockReturnValue(of({
        id: 'evt-2',
        tenantId: 'tenant-123',
        sessionId: 'sess_999',
        visitorId: 'vis_999',
        eventName: 'add_to_cart',
        url: 'https://store.sgrowing.io/cart',
        deviceType: 'Mobile',
        browser: 'Safari Mobile',
        countryCode: 'TN',
        city: 'Tunis',
        timestampUtc: '2026-11-03T10:05:00Z'
      }))
    };

    mockAuth = {
      currentTenant: signal({ id: 'tenant-123', name: 'Test Tenant' })
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi },
        { provide: AuthService, useValue: mockAuth }
      ]
    });

    component = runInInjectionContext(injector, () => new TelemetryStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load live telemetry metrics and recent event stream', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getLiveTelemetry).toHaveBeenCalledWith('tenant-123');
    expect(mockApi.getRecentTelemetryEvents).toHaveBeenCalledWith('tenant-123');
    expect(component.summary()?.activeVisitorsCount).toBe(24);
    expect(component.recentEvents().length).toBe(1);
  });

  it('should simulate client storefront telemetry ingestion and update stream', () => {
    component.simulateTraffic();

    expect(mockApi.ingestTelemetry).toHaveBeenCalledWith(expect.objectContaining({
      tenantId: 'tenant-123',
      countryCode: 'TN'
    }));
    expect(component.recentEvents().length).toBe(2);
    expect(component.recentEvents()[0].eventName).toBe('add_to_cart');
  });

  it('should trigger traffic simulation fallback when recent event stream is empty on load', () => {
    mockApi.getRecentTelemetryEvents.mockReturnValue(of([]));
    component.loadData();

    expect(mockApi.ingestTelemetry).toHaveBeenCalled();
  });

  it('should fallback to default tenant id when currentTenant is null', () => {
    mockAuth.currentTenant.set(null);
    expect(component.tenantId).toBe('00000000-0000-0000-0000-000000000001');
  });
});
