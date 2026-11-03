// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { DealSlippageStudioComponent } from './deal-slippage-studio.component';

describe('DealSlippageStudioComponent (Twenty-Style Opportunity Stage-Gate & Deal Stall Radar)', () => {
  let component: DealSlippageStudioComponent;

  beforeEach(() => {
    component = new DealSlippageStudioComponent();
  });

  it('should initialize with deals and calculate pipeline summary metrics', () => {
    expect(component).toBeTruthy();
    expect(component.deals().length).toBe(3);

    const expectedTotal = 140000 + 65000 + 220000;
    expect(component.totalPipeline()).toBe(expectedTotal);
    expect(component.atRiskAmount()).toBe(140000); // Only deal d-1 has slippageScore >= 50
    expect(component.weightedAmount()).toBe(Math.round(expectedTotal * 0.42));
    expect(component.blockedCount()).toBe(2); // d-1 and d-2 have missingGates
  });

  it('should return appropriate style badge classes for risk classifications', () => {
    expect(component.getRiskBadge('CriticalStalled')).toContain('text-rose-400');
    expect(component.getRiskBadge('HighSlipping')).toContain('text-amber-400');
    expect(component.getRiskBadge('ModerateWatch')).toContain('text-blue-300');
    expect(component.getRiskBadge('LowHealthy')).toContain('text-emerald-400');
  });

  it('should invoke advanceDeal and openNewDealModal handlers without error', () => {
    const deal = component.deals()[0];
    expect(() => component.advanceDeal(deal)).not.toThrow();
    expect(() => component.openNewDealModal()).not.toThrow();
  });
});
