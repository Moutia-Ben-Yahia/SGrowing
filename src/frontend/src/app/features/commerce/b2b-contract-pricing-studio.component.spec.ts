// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { B2BContractPricingStudioComponent } from './b2b-contract-pricing-studio.component';

describe('B2BContractPricingStudioComponent', () => {
  it('should create component with initialized defaults and valid KPIs', () => {
    const comp = new B2BContractPricingStudioComponent();
    expect(comp).toBeTruthy();
    expect(comp.activePriceListsCount()).toBe(2);
    expect(comp.openQuotesCount()).toBe(3);
    expect(comp.pendingApprovalsCount()).toBe(2);
    expect(comp.pipelineVolumeSar()).toBeGreaterThan(50000);
    expect(comp.activeTab()).toBe('calculator');
  });

  it('should calculate correct volume tier discount and gross/net totals for Enterprise Retail Group', () => {
    const comp = new B2BContractPricingStudioComponent();
    comp.selectedAccountId.set('enterprise-retail-group');
    comp.selectedSku.set('SKU-PERFUME-OUD');
    comp.quantity.set(100);

    const calc = comp.calculation();
    expect(calc.appliedPriceListName).toBe('Enterprise Retail Group Contract');
    expect(calc.basePriceSar).toBe(250);
    expect(calc.unitPriceSar).toBe(190);
    expect(calc.totalGrossSar).toBe(25000);
    expect(calc.totalNetSar).toBe(19000);
    expect(calc.discountAmountSar).toBe(6000);
    expect(calc.discountPercent).toBe(24.0);
    expect(calc.routedStatus).toBe('PendingVpApproval');
    expect(calc.requiredRole).toBe('VP_COMMERCE');
    expect(calc.marginPercent).toBeGreaterThanOrEqual(30.0);
  });

  it('should dynamically update calculation when quantity changes across tier bands', () => {
    const comp = new B2BContractPricingStudioComponent();
    comp.selectedAccountId.set('enterprise-retail-group');

    // Tier 1: 1-50 units -> 220 SAR (12% off, PendingManagerApproval)
    comp.setQuantity(25);
    let calc = comp.calculation();
    expect(calc.unitPriceSar).toBe(220);
    expect(calc.discountPercent).toBe(12.0);
    expect(calc.routedStatus).toBe('PendingManagerApproval');
    expect(calc.requiredRole).toBe('SALES_MANAGER');

    // Tier 3: 201+ units -> 160 SAR (36% off, PendingVpApproval)
    comp.setQuantity(250);
    calc = comp.calculation();
    expect(calc.unitPriceSar).toBe(160);
    expect(calc.discountPercent).toBe(36.0);
    expect(calc.routedStatus).toBe('PendingVpApproval');
    expect(calc.requiredRole).toBe('VP_COMMERCE');
  });

  it('should switch to Standard Wholesale list when account is changed', () => {
    const comp = new B2BContractPricingStudioComponent();
    comp.selectedAccountId.set('standard-wholesale');
    comp.selectedSku.set('SKU-PERFUME-OUD');

    // 100 units under standard wholesale: 210 SAR (16% discount)
    comp.setQuantity(100);
    let calc = comp.calculation();
    expect(calc.appliedPriceListName).toBe('Standard B2B Wholesale List');
    expect(calc.unitPriceSar).toBe(210);
    expect(calc.totalNetSar).toBe(21000);
    expect(calc.discountPercent).toBe(16.0);
    expect(calc.routedStatus).toBe('PendingManagerApproval');

    // 20 units under standard wholesale: 240 SAR (4% discount -> AutoApproved)
    comp.setQuantity(20);
    calc = comp.calculation();
    expect(calc.unitPriceSar).toBe(240);
    expect(calc.discountPercent).toBe(4.0);
    expect(calc.routedStatus).toBe('AutoApproved');
    expect(calc.requiredRole).toBe('NONE');
  });

  it('should highlight the matched volume tier curve', () => {
    const comp = new B2BContractPricingStudioComponent();
    comp.setQuantity(100);

    const calc = comp.calculation();
    const activeTier = calc.tiers.find(t => t.isMatched);
    expect(activeTier).toBeDefined();
    expect(activeTier?.minQuantity).toBe(51);
    expect(activeTier?.maxQuantity).toBe(200);

    // Quantity 300 should match 201+
    comp.setQuantity(300);
    const calc300 = comp.calculation();
    const activeTier300 = calc300.tiers.find(t => t.isMatched);
    expect(activeTier300?.minQuantity).toBe(201);
    expect(activeTier300?.maxQuantity).toBeNull();
  });

  it('should create new quote from calculator and navigate to quotes tab', () => {
    const comp = new B2BContractPricingStudioComponent();
    const initialCount = comp.quotes().length;
    comp.contactName.set('Faisal Al-Dosari');
    comp.contactEmail.set('faisal@aldosari.sa');
    comp.setQuantity(50);

    comp.createQuoteFromCalculator();

    expect(comp.quotes().length).toBe(initialCount + 1);
    const created = comp.quotes()[0];
    expect(created.contactName).toBe('Faisal Al-Dosari');
    expect(created.contactEmail).toBe('faisal@aldosari.sa');
    expect(created.items[0].quantity).toBe(50);
    expect(comp.activeTab()).toBe('quotes');
  });

  it('should approve quote and update status and role', () => {
    const comp = new B2BContractPricingStudioComponent();
    const pendingBefore = comp.pendingApprovalsCount();

    comp.approveQuote('qt-1002');

    const updated = comp.quotes().find(q => q.quoteId === 'qt-1002');
    expect(updated?.status).toBe('Approved');
    expect(updated?.requiredApproverRole).toBe('COMPLETED');
    expect(comp.pendingApprovalsCount()).toBe(pendingBefore - 1);
  });

  it('should reject quote and update status and role', () => {
    const comp = new B2BContractPricingStudioComponent();
    const pendingBefore = comp.pendingApprovalsCount();

    comp.rejectQuote('qt-1001');

    const updated = comp.quotes().find(q => q.quoteId === 'qt-1001');
    expect(updated?.status).toBe('Rejected');
    expect(updated?.requiredApproverRole).toBe('REJECTED');
    expect(comp.pendingApprovalsCount()).toBe(pendingBefore - 1);
  });

  it('should switch tabs reactively', () => {
    const comp = new B2BContractPricingStudioComponent();
    expect(comp.activeTab()).toBe('calculator');

    comp.setTab('pricelists');
    expect(comp.activeTab()).toBe('pricelists');

    comp.setTab('quotes');
    expect(comp.activeTab()).toBe('quotes');

    comp.setTab('calculator');
    expect(comp.activeTab()).toBe('calculator');
  });

  it('should filter quotes by status correctly', () => {
    const comp = new B2BContractPricingStudioComponent();
    expect(comp.filteredQuotes().length).toBe(comp.quotes().length);

    comp.setStatusFilter('Approved');
    expect(comp.filteredQuotes().every(q => q.status === 'Approved')).toBe(true);

    comp.setStatusFilter('PendingManagerApproval');
    expect(comp.filteredQuotes().every(q => q.status === 'PendingManagerApproval')).toBe(true);

    comp.setStatusFilter('ALL');
    expect(comp.filteredQuotes().length).toBe(comp.quotes().length);
  });
});
