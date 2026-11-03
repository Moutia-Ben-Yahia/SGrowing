// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { IdempotencyLedgerCockpitComponent, IdempotencyRecord } from './idempotency-ledger-cockpit.component';

describe('IdempotencyLedgerCockpitComponent (Enterprise Distributed Idempotency Ledger)', () => {
  let component: IdempotencyLedgerCockpitComponent;
  let mockHttp: any;

  const mockRecords: IdempotencyRecord[] = [
    {
      key: 'idem_pi_9f81a7b2c01d',
      tenantId: 'tenant-default',
      httpMethod: 'POST',
      requestPath: '/api/v1/payments/intents',
      fingerprint: '3b89a1c8902f4a8b792100df124e88319a99887766554433221100ffeeddccba',
      status: 'Completed',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      expiresAt: new Date(Date.now() + 86400000).toISOString(),
      lockTtlSeconds: 86400,
      responseStatusCode: 200,
      responseHeaders: { 'Content-Type': 'application/json' },
      responseBody: '{"id": "pi_123", "amount": 14999}',
      replayCount: 3,
      isExpired: false
    },
    {
      key: 'idem_ord_conflict_123',
      tenantId: 'tenant-default',
      httpMethod: 'POST',
      requestPath: '/api/v1/orders/checkout',
      fingerprint: 'fprint2',
      status: 'InProgress',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      expiresAt: new Date(Date.now() + 60000).toISOString(),
      lockTtlSeconds: 120,
      responseStatusCode: null,
      responseHeaders: null,
      responseBody: null,
      replayCount: 0,
      isExpired: false
    }
  ];

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/ledger')) {
          return of({
            records: mockRecords,
            totalCount: 2,
            metrics: {
              totalTrackedKeys: 2,
              replayHits: 3,
              conflictsDetected: 1,
              mismatchesBlocked: 0,
              replayHitRatePct: 60.0,
              totalRequestsProcessed: 5,
              activeLocksCount: 1,
              completedCount: 1
            }
          });
        }
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/simulate')) {
          return of({
            statusCode: 200,
            success: true,
            replayDetected: body?.mode === 'replay',
            status: 'Completed',
            message: 'Simulation completed',
            headers: { 'X-Idempotency-Key': body?.idempotencyKey },
            responseBody: '{"simulated": true}',
            replayCount: 1,
            executionDurationMs: 4,
            metrics: {
              totalTrackedKeys: 2,
              replayHits: 4,
              conflictsDetected: 1,
              mismatchesBlocked: 0,
              replayHitRatePct: 66.0,
              totalRequestsProcessed: 6,
              activeLocksCount: 1,
              completedCount: 1
            }
          });
        }
        return of({});
      }),
      delete: vi.fn().mockImplementation(() => of({ success: true }))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new IdempotencyLedgerCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize with records and metrics', () => {
    expect(component).toBeTruthy();
    expect(component.records().length).toBe(2);
    expect(component.metrics().totalTrackedKeys).toBe(2);
    expect(component.loading()).toBe(false);
  });

  it('should filter records by search query and status filter', () => {
    component.searchQuery = 'payments';
    component.records.update(r => [...r]);
    expect(component.filteredRecords().length).toBe(1);
    expect(component.filteredRecords()[0].key).toBe('idem_pi_9f81a7b2c01d');

    component.searchQuery = '';
    component.statusFilter = 'InProgress';
    component.records.update(r => [...r]);
    expect(component.filteredRecords().length).toBe(1);
    expect(component.filteredRecords()[0].status).toBe('InProgress');
  });

  it('should select record for simulator cockpit', () => {
    const row = mockRecords[0];
    component.selectRecordForSimulator(row);

    expect(component.simKey).toBe(row.key);
    expect(component.simMethod).toBe(row.httpMethod);
    expect(component.simPath).toBe(row.requestPath);
  });

  it('should execute simulation and update last response', () => {
    component.runSimulation('replay');

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.lastResponse()).not.toBeNull();
    expect(component.lastResponse()?.replayDetected).toBe(true);
  });

  it('should return correct badge classes for status codes and HTTP methods', () => {
    expect(component.getStatusBadgeClass(200)).toContain('text-emerald-300');
    expect(component.getStatusBadgeClass(409)).toContain('text-amber-300');
    expect(component.getStatusBadgeClass(422)).toContain('text-rose-300');

    expect(component.getStatusClass('Completed')).toContain('text-emerald-400');
    expect(component.getStatusClass('InProgress')).toContain('text-amber-400');

    expect(component.getMethodBadgeClass('POST')).toContain('text-violet-300');
    expect(component.getMethodBadgeClass('PUT')).toContain('text-sky-300');
  });
});
