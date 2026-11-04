// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ReplenishmentCockpitComponent } from './replenishment-cockpit.component';

describe('ReplenishmentCockpitComponent', () => {
  let component: ReplenishmentCockpitComponent;

  beforeEach(() => {
    component = new ReplenishmentCockpitComponent();
  });

  it('should create component and evaluate replenishment recommendations', () => {
    expect(component).toBeTruthy();
    expect(component.recommendations().length).toBeGreaterThan(0);
    expect(component.criticalDepletionsCount()).toBeGreaterThan(0);
    expect(component.totalStagedCost()).toBeGreaterThan(0);
  });

  it('should generate an ANSI X12 EDI 850 purchase order', () => {
    component.generateEdiPurchaseOrder();
    const po = component.stagedPo();

    expect(po).toBeTruthy();
    AssertContains(po?.edi850RawPayload ?? '', 'ISA*00*');
    AssertContains(po?.edi850RawPayload ?? '', 'ST*850*');
    expect(po?.status).toBe('Staged');
  });

  it('should dispatch staged purchase order to supplier mesh', () => {
    expect(component.stagedPo()?.status).toBe('Staged');
    component.dispatchPoToSupplier();
    expect(component.stagedPo()?.status).toBe('Dispatched');
  });

  it('should allow selecting a replenishment recommendation and compute totalStagedCost', () => {
    const second = component.recommendations()[1];
    component.selectRecommendation(second);
    expect(component.selectedRecommendation()?.skuId).toBe(second.skuId);
    expect(component.totalStagedCost()).toBe(
      component.recommendations().reduce((acc, r) => acc + r.estimatedCost, 0)
    );
  });

  it('should compute criticalDepletionsCount and handle empty recommendations', () => {
    expect(component.criticalDepletionsCount()).toBe(1);
    component.recommendations.set([]);
    expect(component.criticalDepletionsCount()).toBe(0);
    expect(component.totalStagedCost()).toBe(0);

    component.stagedPo.set(null);
    expect(() => component.dispatchPoToSupplier()).not.toThrow();
    expect(component.stagedPo()).toBeNull();
  });

  function AssertContains(actual: string, expected: string): void {
    expect(actual.includes(expected)).toBe(true);
  }
});
