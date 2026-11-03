// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { CpqQuotationStudioComponent, PriceBookUi, CpqQuoteUi } from './cpq-quotation-studio.component';

describe('CpqQuotationStudioComponent', () => {
  let component: CpqQuotationStudioComponent;
  let mockHttp: any;

  const mockPriceBooks: PriceBookUi[] = [
    {
      id: 'pb-enterprise-2026',
      name: 'Standard Enterprise 2026 Price Book',
      currency: 'USD',
      isDefault: true,
      priceBookEntries: [
        {
          id: 'e1',
          productSku: 'ENTERPRISE-CRM-LIC',
          productName: 'Enterprise Growth CRM Seat',
          basePrice: 120,
          costPrice: 30,
          volumeTiers: [
            { minQuantity: 1, maxQuantity: 9, unitPrice: 120 },
            { minQuantity: 10, maxQuantity: 49, unitPrice: 100 },
            { minQuantity: 50, maxQuantity: null, unitPrice: 80 }
          ]
        }
      ]
    }
  ];

  const mockQuotes: CpqQuoteUi[] = [
    {
      id: 'q-101',
      tenantId: 'tenant-123',
      quoteNumber: 'Q-2026-081',
      customerId: 'cust-1',
      customerName: 'Acme Corporation',
      status: 'Draft',
      currency: 'USD',
      subtotal: 1000,
      discountTotal: 100,
      taxTotal: 0,
      grandTotal: 900,
      overallMarginPercentage: 66.7,
      requiresManagerApproval: false,
      lineItems: [],
      expirationDateUtc: '2026-12-01T00:00:00Z',
      termsAndConditions: 'Standard 30 days',
      checksumSha256: 'abc123def456',
      createdAtUtc: '2026-11-03T10:00:00Z'
    }
  ];

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/price-books')) return of(mockPriceBooks);
        if (url.includes('/quotes')) return of(mockQuotes);
        return of({});
      }),
      post: vi.fn().mockReturnValue(of({ success: true }))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new CpqQuotationStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load price books and quotes list', () => {
    expect(component).toBeTruthy();
    expect(mockHttp.get).toHaveBeenCalledWith('/api/v1/sales/cpq/price-books');
    expect(component.priceBooks().length).toBe(1);
    expect(component.selectedPriceBookId()).toBe('pb-enterprise-2026');
    expect(component.quotesList().length).toBe(1);
  });

  it('should evaluate volume tiered unit pricing based on quantity', () => {
    const entry = mockPriceBooks[0].priceBookEntries[0];

    // Qty 5 -> Tier 1 (1-9) -> $120
    expect(component.evaluateTierPrice(entry, 5)).toBe(120);

    // Qty 25 -> Tier 2 (10-49) -> $100
    expect(component.evaluateTierPrice(entry, 25)).toBe(100);

    // Qty 100 -> Tier 3 (50+) -> $80
    expect(component.evaluateTierPrice(entry, 100)).toBe(80);
  });

  it('should add, update, and recalculate line items and margin percentages', () => {
    component.draftLines.set([]);
    component.addLineItem();

    expect(component.draftLines().length).toBe(1);
    const line = component.draftLines()[0];
    expect(line.productSku).toBe('ENTERPRISE-CRM-LIC');

    // Update quantity to 15 (should trigger tier pricing to $100)
    component.onQuantityChanged(0, 15);
    expect(component.draftLines()[0].unitPrice).toBe(100);
    expect(component.draftLines()[0].lineTotal).toBe(1500);

    // Apply 10% discount
    component.onDiscountChanged(0, 10);
    expect(component.draftLines()[0].discountPercentage).toBe(10);
    expect(component.draftLines()[0].lineTotal).toBe(1350);
    expect(component.computedGrandTotal()).toBe(1350);
  });

  it('should flag manager approval requirement when discount exceeds policy (>20%)', () => {
    component.draftLines.set([]);
    component.addLineItem();

    component.onDiscountChanged(0, 15);
    expect(component.discountExceedsPolicy()).toBe(false);

    component.onDiscountChanged(0, 25);
    expect(component.discountExceedsPolicy()).toBe(true);
  });
});
