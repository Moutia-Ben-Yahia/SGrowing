// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { ServerSideAttributionCockpitComponent } from './server-side-attribution-cockpit.component';

describe('ServerSideAttributionCockpitComponent', () => {
  let component: ServerSideAttributionCockpitComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue(of({
        platforms: [],
        recovery: {
          liftPercent: 32.4,
          recoveredConversions: 840,
          recoveredRevenue: 64200,
          clientSideBlockedRate: 28.5,
          totalTrackedRevenue: 248000
        },
        recentOutbox: []
      })),
      post: vi.fn().mockReturnValue(of({
        dispatchedCount: 3,
        failedCount: 0,
        testTraceId: 'TEST-TRACE-OK',
        responseSummary: 'Simulated CAPI response'
      }))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new ServerSideAttributionCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize and load default pre-seeded CAPI outbox items', () => {
    expect(component).toBeTruthy();
    expect(mockHttp.get).toHaveBeenCalled();
    const outbox = component.outboxItems();
    expect(outbox.length).toBeGreaterThanOrEqual(4);
    expect(component.filteredOutboxItems().length).toBeGreaterThan(0);
  });

  it('should filter outbox records by platform, status, and event name', () => {
    component.filterPlatform = 'Meta';
    component.outboxItems.update(items => [...items]);
    const metaItems = component.filteredOutboxItems();
    expect(metaItems.every(i => i.platform === 'Meta')).toBe(true);

    component.filterPlatform = 'ALL';
    component.filterStatus = 'Pending';
    component.outboxItems.update(items => [...items]);
    const pendingItems = component.filteredOutboxItems();
    expect(pendingItems.every(i => i.status === 'Pending')).toBe(true);
  });

  it('should dispatch pending CAPI outbox items to ad platforms', () => {
    component.dispatchPendingOutbox();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/attribution/capi/dispatch-postbacks',
      expect.any(Object)
    );
    expect(component.isDispatching()).toBe(false);
  });

  it('should execute test pixel handshake and store diagnostic trace', () => {
    component.openTestPixelModal();
    expect(component.showTestModal()).toBe(true);

    component.testPlatform = 'TikTok';
    component.runTestHandshake();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.isTesting()).toBe(false);
    expect(component.testResult()).toBeDefined();
    expect(component.testResult()?.testTraceId).toBe('TEST-TRACE-OK');
  });

  it('should generate deterministic 64-char SHA-256 style hash for user identities', () => {
    const hash = component['hashStringSync']('customer@sgrowing.io');
    expect(hash.length).toBe(64);
    expect(/^[0-9a-f]{64}$/.test(hash)).toBe(true);
  });
});
