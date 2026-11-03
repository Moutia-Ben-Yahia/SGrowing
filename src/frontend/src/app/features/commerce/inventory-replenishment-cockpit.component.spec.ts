// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { InventoryReplenishmentCockpitComponent } from './inventory-replenishment-cockpit.component';

describe('InventoryReplenishmentCockpitComponent (Multi-Warehouse Replenishment Engine)', () => {
  let component: InventoryReplenishmentCockpitComponent;
  let mockHttp: any;

  const mockPo = {
    id: 'po-seed-101',
    poNumber: 'PO-2026-101',
    vendorId: 'v-1',
    vendorName: 'Global Vendor',
    vendorContactEmail: 'orders@global.com',
    destinationWarehouseId: 'wh-north',
    destinationWarehouseName: 'North Distribution Hub',
    status: 'Draft' as const,
    items: [],
    totalUnits: 100,
    subtotalUsd: 5000,
    taxAmountUsd: 0,
    shippingFeeUsd: 200,
    totalAmountUsd: 5200,
    currency: 'USD',
    estimatedLeadTimeDays: 5,
    expectedDeliveryDateUtc: null,
    trackingNumber: null,
    notes: null,
    createdBy: 'System',
    createdAtUtc: new Date().toISOString(),
    submittedAtUtc: null,
    receivedAtUtc: null
  };

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/purchase-orders')) {
          return of({ success: true, purchaseOrders: [mockPo] });
        }
        if (url.includes('/items')) {
          return of({ success: false }); // keep seed items
        }
        if (url.includes('/transfers/recommendations')) {
          return of({ success: false }); // keep seed recommendations
        }
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/generate-autonomous')) {
          return of({
            success: true,
            purchaseOrders: [
              {
                ...mockPo,
                id: 'po-test-auto-1',
                poNumber: 'PO-2026-AUTO-901'
              }
            ]
          });
        }
        if (url.includes('/recalculate')) {
          return of({ success: true, result: {} });
        }
        return of({});
      }),
      patch: vi.fn().mockImplementation((url: string, body: any) => {
        return of({
          success: true,
          purchaseOrder: {
            ...mockPo,
            id: 'po-seed-101',
            status: body?.nextStatus || 'SubmittedToVendor',
            trackingNumber: body?.trackingNumber || null
          }
        });
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new InventoryReplenishmentCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize with in-memory seed inventory items, POs, and transfer proposals', () => {
    expect(component).toBeTruthy();
    expect(component.items().length).toBeGreaterThan(0);
    expect(component.purchaseOrders().length).toBeGreaterThan(0);
    expect(component.transferProposals().length).toBeGreaterThan(0);
    expect(component.activeTab()).toBe('inventory');
  });

  it('should switch navigation tabs reactively', () => {
    component.activeTab.set('purchaseOrders');
    expect(component.activeTab()).toBe('purchaseOrders');

    component.activeTab.set('transfers');
    expect(component.activeTab()).toBe('transfers');

    component.activeTab.set('analytics');
    expect(component.activeTab()).toBe('analytics');
  });

  it('should trigger autonomous PO generation and prepend generated orders', () => {
    const initialPoCount = component.purchaseOrders().length;
    component.triggerAutonomousGeneration();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.purchaseOrders().length).toBe(initialPoCount + 1);
    expect(component.purchaseOrders()[0].poNumber).toBe('PO-2026-AUTO-901');
  });

  it('should recalculate inventory metrics across warehouses', () => {
    component.recalculateAllMetrics();

    expect(mockHttp.post).toHaveBeenCalled();
  });

  it('should transition purchase order status', () => {
    const firstPo = component.purchaseOrders()[0];
    component.transitionPoStatus(firstPo.id, 'SubmittedToVendor', 'TRACK-12345');

    expect(mockHttp.patch).toHaveBeenCalled();
  });
});
