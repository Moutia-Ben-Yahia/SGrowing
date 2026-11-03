// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { of } from 'rxjs';
import { CpqBuilderComponent } from './cpq-builder.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { TranslationService } from '../../core/services/translation.service';

describe('CpqBuilderComponent (Enterprise CPQ Studio & Polymorphic Pricing)', () => {
  let component: CpqBuilderComponent;
  let mockApi: any;
  let mockAuth: any;
  let mockTranslation: any;

  beforeEach(() => {
    mockApi = {
      createCpqQuote: vi.fn().mockReturnValue(of({
        id: 'quote-test-101',
        quoteNumber: 'CPQ-2026-9901',
        customerName: 'Enterprise Logistics Corp',
        customerEmail: 'finance@carthage-logistics.tn',
        currency: 'USD',
        subtotal: 18840,
        discountAmount: 1884,
        taxAmount: 3221.64,
        grandTotal: 20177.64,
        status: 'Generated',
        lineItems: [],
        createdAtUtc: new Date().toISOString()
      }))
    };

    mockAuth = {
      currentTenant: signal({ id: 'tenant-test-1', name: 'Test Tenant' })
    };

    mockTranslation = {
      currentLocale: signal('en-US'),
      setLocale: vi.fn()
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi },
        { provide: AuthService, useValue: mockAuth },
        { provide: TranslationService, useValue: mockTranslation }
      ]
    });

    component = runInInjectionContext(injector, () => new CpqBuilderComponent());
    component.ngOnInit();
  });

  it('should initialize with default customer info and seed line items', () => {
    expect(component).toBeTruthy();
    expect(component.customerName).toBe('Acme Logistics Global');
    expect(component.selectedCurrency).toBe('USD');
    expect(component.lineItems().length).toBe(2);
    expect(component.globalDiscountPercentage).toBe(10);
  });

  it('should calculate line item totals and grand totals correctly', () => {
    const item = component.lineItems()[0]; // 1 unit @ 12000, 0% discount = 12000
    expect(component.calculateLineTotal(item)).toBe(12000);

    const subtotal = component.computedSubtotal();
    expect(subtotal).toBe(18840);

    const discount = component.computedDiscount();
    expect(discount).toBe(1884); // 10% of 18840

    const tax = component.computedTax();
    expect(tax).toBeCloseTo((18840 - 1884) * 0.19, 2);

    const grandTotal = component.computedGrandTotal();
    expect(grandTotal).toBeCloseTo((18840 - 1884) * 1.19, 2);
  });

  it('should add a new line item and increase total count', () => {
    const initialCount = component.lineItems().length;
    component.addLineItem();

    expect(component.lineItems().length).toBe(initialCount + 1);
    const addedItem = component.lineItems()[initialCount];
    expect(addedItem.sku).toContain('SG-ADDON-');
    expect(addedItem.unitPrice).toBe(500);
  });

  it('should remove a line item by index', () => {
    const initialCount = component.lineItems().length;
    component.removeLineItem(0);

    expect(component.lineItems().length).toBe(initialCount - 1);
  });

  it('should call ApiService and update generatedQuote upon quote generation', () => {
    component.generateQuote();

    expect(mockApi.createCpqQuote).toHaveBeenCalled();
    expect(component.isGeneratingQuote()).toBe(false);
    expect(component.generatedQuote()).not.toBeNull();
    expect(component.generatedQuote()?.quoteNumber).toBe('CPQ-2026-9901');
  });
});
