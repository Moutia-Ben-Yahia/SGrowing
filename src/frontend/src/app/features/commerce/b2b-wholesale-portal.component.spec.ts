// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { B2BWholesalePortalComponent } from './b2b-wholesale-portal.component';

describe('B2BWholesalePortalComponent (Wholesale B2B Pricing & Credit Matrix)', () => {
  let component: B2BWholesalePortalComponent;

  beforeEach(() => {
    const mockHttp = {
      get: () => ({ subscribe: () => {} }),
      post: () => ({ subscribe: () => {} }),
      put: () => ({ subscribe: () => {} }),
      delete: () => ({ subscribe: () => {} })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new B2BWholesalePortalComponent());
    component.ngOnInit();
  });

  it('should initialize with corporate accounts and default Acme Industrial selected', () => {
    expect(component.accounts().length).toBeGreaterThanOrEqual(2);
    const selected = component.selectedAccount();
    expect(selected).toBeDefined();
    expect(selected.companyName).toBe('Acme Industrial Logistics Corp');
    expect(selected.creditLimit).toBe(100000);
    expect(selected.paymentTermsDays).toBe(30);
  });

  it('should calculate initial core sizes matrix volume and tier discount correctly', () => {
    // fillCoreSizes() sets 100 units per color across 4 colors = 400 total units
    expect(component.totalMatrixUnits()).toBe(400);

    // 400 units falls in Tier 3: 200-499 units -> 18% OFF
    const tier = component.appliedPricingTier();
    expect(tier.discountPercent).toBe(18);
    expect(tier.tierLabel).toContain('18% OFF');

    // Unit price: Base $28.50 * (1 - 0.18) = $23.37
    expect(component.discountedUnitPrice()).toBe(23.37);

    // Subtotal: 400 * 23.37 = $9,348.00
    expect(component.matrixSubtotal()).toBe(9348.0);

    // 19% VAT on $9,348.00 = $1,776.12
    expect(component.matrixTaxAmount()).toBe(1776.12);

    // Shipping fee is $45 for < 500 units
    expect(component.matrixShippingFee()).toBe(45);

    // Grand total: 9348.00 + 1776.12 + 45 = $11,169.12
    expect(component.matrixGrandTotal()).toBe(11169.12);
  });

  it('should reset all matrix cells to zero on clearMatrix', () => {
    component.clearMatrix();

    expect(component.totalMatrixUnits()).toBe(0);
    expect(component.matrixSubtotal()).toBe(0);
    expect(component.matrixTaxAmount()).toBe(0);
    expect(component.matrixShippingFee()).toBe(0);
    expect(component.matrixGrandTotal()).toBe(0);
    expect(component.appliedPricingTier().discountPercent).toBe(0);
  });

  it('should grant free freight shipping when volume reaches 500 units', () => {
    // 25 units per cell * 20 cells = 500 units
    component.applyQuickFill(25);

    expect(component.totalMatrixUnits()).toBe(500);
    // Tier 4: 500-999 units -> 25% OFF
    expect(component.appliedPricingTier().discountPercent).toBe(25);
    // Free freight above 500 units
    expect(component.matrixShippingFee()).toBe(0);
  });

  it('should evaluate revolving credit and spending approval requirements reactively', () => {
    const status = component.creditStatus();
    // Available credit: $100,000 - $21,400 (unpaid) - $20,301.88 (pending holds) = $58,298.12
    expect(status.creditLimit).toBe(100000);
    expect(status.unpaidInvoicesTotal).toBe(21400);
    expect(status.availableCredit).toBe(58298.12);
    expect(status.isCreditExhausted).toBe(false);

    // Officer Jean Dupont has $10,000 spending limit.
    // Core sizes grand total ($11,169.12) exceeds $10,000, so requiresApproval must be true
    expect(component.isExceedingSpendingLimit()).toBe(true);
    expect(component.requiresApproval()).toBe(true);
  });

  it('should approve a pending purchase order and update warehouse stock reservation', () => {
    const pendingOrders = component.pendingApprovals();
    expect(pendingOrders.length).toBeGreaterThan(0);
    const targetOrder = pendingOrders[0];

    component.approveOrder(targetOrder.id);

    const updated = component.purchaseOrders().find(o => o.id === targetOrder.id);
    expect(updated?.status).toBe('Confirmed');
    expect(updated?.warehouseStockReserved).toBe(true);
    expect(updated?.approvedByOfficerId).toBeDefined();
  });
});
