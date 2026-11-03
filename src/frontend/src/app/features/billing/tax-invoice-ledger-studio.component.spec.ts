// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { TaxInvoiceLedgerStudioComponent } from './tax-invoice-ledger-studio.component';
import { of } from 'rxjs';

describe('TaxInvoiceLedgerStudioComponent', () => {
  let component: TaxInvoiceLedgerStudioComponent;
  let httpMock: any;

  beforeEach(() => {
    httpMock = {
      get: vi.fn().mockReturnValue(of([])),
      post: vi.fn().mockReturnValue(of({})),
      put: vi.fn().mockReturnValue(of({}))
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpMock }
    ]);

    injector.runInContext(() => {
      component = new TaxInvoiceLedgerStudioComponent();
    });

    component.ngOnInit();
  });

  it('should initialize with pre-seeded currencies, tax jurisdictions, and invoices', () => {
    expect(component).toBeTruthy();
    expect(component.activeTab()).toBe('invoices');
    expect(component.currencies().length).toBeGreaterThan(0);
    expect(component.jurisdictions().length).toBeGreaterThan(0);
    expect(component.invoices().length).toBeGreaterThan(0);
    expect(component.totalInvoicedUsd()).toBeGreaterThan(0);
  });

  it('should toggle ledger studio tabs', () => {
    component.activeTab.set('credit-notes');
    expect(component.activeTab()).toBe('credit-notes');

    component.activeTab.set('tax-liabilities');
    expect(component.activeTab()).toBe('tax-liabilities');

    component.activeTab.set('sandbox');
    expect(component.activeTab()).toBe('sandbox');
  });

  it('should compute tax evaluation under EU B2B reverse charge rules', () => {
    component.sandboxMerchantCountry = 'DE';
    component.sandboxCustomerCountry = 'FR';
    component.sandboxIsB2B = true;
    component.sandboxVatNumber = 'FR32123456789';
    component.sandboxSubtotal = 10000;

    component.runSandboxEvaluation();

    const res = component.sandboxResult();
    expect(res).toBeTruthy();
    expect(res?.isReverseCharge).toBe(true);
    expect(res?.applicableTaxRatePct).toBe(0);
    expect(res?.taxAmount).toBe(0);
  });

  it('should compute foreign exchange conversion accurately using spot rates', () => {
    component.fxAmount = 100;
    component.fxFrom = 'USD';
    component.fxTo = 'EUR';

    const converted = component.calculatedFxConversion();
    expect(converted).toBeTruthy();
    expect(parseFloat(converted.replace(/,/g, ''))).toBeGreaterThan(80);
  });

  it('should filter invoices by status', () => {
    component.selectedStatusFilter.set('Paid');
    const filtered = component.filteredInvoices();
    expect(filtered.every(i => i.status === 'Paid')).toBe(true);
  });
});
