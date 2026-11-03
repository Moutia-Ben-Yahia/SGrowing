// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { InvoiceManagementCockpitComponent } from './invoice-management-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('InvoiceManagementCockpitComponent', () => {
  let component: InvoiceManagementCockpitComponent;

  beforeEach(() => {
    const injector = createEnvironmentInjector([TranslationService]);

    injector.runInContext(() => {
      component = new InvoiceManagementCockpitComponent();
    });
  });

  it('should initialize with multi-currency rates, enterprise invoices, and financial totals', () => {
    expect(component).toBeTruthy();
    expect(component.currencyRates().length).toBeGreaterThan(0);
    expect(component.invoices().length).toBeGreaterThan(0);
    expect(component.totalInvoicedUsd()).toBeGreaterThan(0);
    expect(component.paidCount()).toBeGreaterThanOrEqual(1);
  });

  it('should convert currencies via exchange rate calculator', () => {
    component.calcAmount = 1000;
    component.calcFrom = 'USD';
    component.calcTo = 'TND'; // 3.12 rate

    const result = parseFloat(component.calculatedConversion());
    expect(result).toBeCloseTo(3120, 0);
  });

  it('should filter invoices by settlement status', () => {
    component.statusFilter.set('Paid');
    const filtered = component.filteredInvoices();
    expect(filtered.every(i => i.status === 'Paid')).toBe(true);

    component.statusFilter.set('All');
    expect(component.filteredInvoices().length).toBe(component.invoices().length);
  });

  it('should mark invoice as paid and recalculate collected revenue', () => {
    const unpaid = component.invoices().find(i => i.status !== 'Paid');
    expect(unpaid).toBeTruthy();

    const initialPaidCount = component.paidCount();
    component.markAsPaid(unpaid!);

    const updated = component.invoices().find(i => i.id === unpaid!.id);
    expect(updated?.status).toBe('Paid');
    expect(component.paidCount()).toBe(initialPaidCount + 1);
  });

  it('should generate new invoice and append to ledger records', () => {
    const initialCount = component.invoices().length;

    component.newClientName = 'Hannibal Telecom';
    component.newClientEmail = 'billing@hannibal-tel.tn';
    component.newCurrency = 'USD';
    component.newItemDesc = 'Dedicated SIP Softphone Trunk';
    component.newItemPrice = 1200;

    component.submitManualInvoice();

    expect(component.invoices().length).toBe(initialCount + 1);
    expect(component.invoices()[0].clientName).toBe('Hannibal Telecom');
    expect(component.openCreateModal()).toBe(false);
  });
});
