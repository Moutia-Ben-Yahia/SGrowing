import '@angular/compiler';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { CourierOfflineSyncService, CourierDeliveryItem, CourierDeliverySyncBatchResult } from './courier-offline-sync.service';

describe('CourierOfflineSyncService', () => {
  let service: CourierOfflineSyncService;
  let mockHttp: any;
  let injector: Injector;

  const mockItem: CourierDeliveryItem = {
    parcelId: 'parcel-101',
    trackingNumber: 'YAL-2001',
    courierDriverId: 'driver-dz-01',
    wilayaCode: '16',
    status: 'Delivered',
    cashCollected: 4500,
    customerSignatureHash: 'sig-abc-123',
    recordedAtUtc: '2026-11-05T12:00:00Z'
  };

  beforeEach(() => {
    localStorage.clear();

    mockHttp = {
      get: vi.fn(),
      post: vi.fn()
    };

    injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    service = runInInjectionContext(injector, () => new CourierOfflineSyncService());
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should initialize with empty queue and online state', () => {
    expect(service.pendingCount()).toBe(0);
    expect(service.pendingDeliveries()).toEqual([]);
    expect(service.isOnline()).toBe(true);
    expect(service.isSyncing()).toBe(false);
  });

  it('should queue delivery and persist to localStorage', () => {
    service.queueDelivery(mockItem);

    expect(service.pendingCount()).toBe(1);
    expect(service.pendingDeliveries()[0].parcelId).toBe('parcel-101');

    const stored = JSON.parse(localStorage.getItem('sgrowing_courier_offline_queue') || '[]');
    expect(stored.length).toBe(1);
    expect(stored[0].trackingNumber).toBe('YAL-2001');
  });

  it('should update online/offline state via handleNetworkChange', () => {
    service.handleNetworkChange(false);
    expect(service.isOnline()).toBe(false);

    service.handleNetworkChange(true);
    expect(service.isOnline()).toBe(true);
  });

  it('should sync pending deliveries and clear synced items from queue', async () => {
    service.queueDelivery(mockItem);

    const mockResponse: CourierDeliverySyncBatchResult = {
      totalReceived: 1,
      successfullySynced: 1,
      duplicatesIgnored: 0,
      totalCashReconciled: 4500,
      syncedParcelIds: ['parcel-101']
    };
    mockHttp.post.mockReturnValue(of(mockResponse));

    const result = await service.syncPendingDeliveries('tenant-alpha-01');

    expect(mockHttp.post).toHaveBeenCalledWith('/api/v1/courier/deliveries/sync', {
      tenantId: 'tenant-alpha-01',
      deliveries: [mockItem]
    });
    expect(result).toEqual(mockResponse);
    expect(service.pendingCount()).toBe(0);
    expect(service.pendingDeliveries()).toEqual([]);
  });

  it('should return null and skip HTTP when offline', async () => {
    service.queueDelivery(mockItem);
    service.handleNetworkChange(false);

    const result = await service.syncPendingDeliveries('tenant-alpha-01');

    expect(result).toBeNull();
    expect(service.pendingCount()).toBe(1);
    expect(mockHttp.post).not.toHaveBeenCalled();
  });

  it('should retain queue when sync request fails with server error', async () => {
    service.queueDelivery(mockItem);

    mockHttp.post.mockReturnValue(throwError(() => new Error('Server 500 error')));

    const result = await service.syncPendingDeliveries('tenant-alpha-01');

    expect(result).toBeNull();
    expect(service.pendingCount()).toBe(1);
    expect(service.isSyncing()).toBe(false);
  });

  it('should clear queue completely', () => {
    service.queueDelivery(mockItem);
    expect(service.pendingCount()).toBe(1);

    service.clearQueue();

    expect(service.pendingCount()).toBe(0);
    expect(service.pendingDeliveries()).toEqual([]);
    const stored = JSON.parse(localStorage.getItem('sgrowing_courier_offline_queue') || '[]');
    expect(stored.length).toBe(0);
  });
});
