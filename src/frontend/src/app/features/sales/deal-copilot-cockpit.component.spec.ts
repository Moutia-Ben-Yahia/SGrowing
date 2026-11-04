// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { DealCopilotCockpitComponent } from './deal-copilot-cockpit.component';

describe('DealCopilotCockpitComponent', () => {
  let component: DealCopilotCockpitComponent;

  beforeEach(() => {
    component = new DealCopilotCockpitComponent();
  });

  it('should initialize with pipeline deals and aggregate health metrics', () => {
    expect(component).toBeTruthy();
    expect(component.deals().length).toBeGreaterThanOrEqual(3);
    expect(component.totalPipeline()).toBeGreaterThan(500000);
    expect(component.aggregateHealth()).toBeGreaterThan(50);
  });

  it('should execute intervention, lift win probability and update risk badge', () => {
    const slippingDeal = component.deals().find(d => d.dealId === 'deal_quantum_corp')!;
    const initialProb = slippingDeal.winProbability;

    component.executeIntervention(slippingDeal);

    const updated = component.deals().find(d => d.dealId === 'deal_quantum_corp')!;
    expect(updated.winProbability).toBeGreaterThan(initialProb);
    expect(updated.riskLevel).toBe('Healthy');
    expect(updated.daysInactive).toBe(0);
    expect(component.actionToast()).toContain('Successfully dispatched AI intervention');
  });

  it('should calculate total pipeline and resolve all at-risk deals via autonomous AI intervention', () => {
    expect(component.totalPipeline()).toBe(599000); // 185000 + 320000 + 94000
    expect(component.atRiskCount()).toBe(2);

    const moderateDeal = component.deals().find(d => d.riskLevel === 'ModerateRisk')!;
    component.executeIntervention(moderateDeal);

    const severeDeal = component.deals().find(d => d.riskLevel === 'SevereRisk')!;
    component.executeIntervention(severeDeal);

    expect(component.atRiskCount()).toBe(0);
  });

  it('should return 0 metrics when deals list is empty and return correct risk badge classes', () => {
    expect(component.getRiskBadgeClass('SevereRisk')).toBe('bg-rose-100 text-rose-800');
    expect(component.getRiskBadgeClass('ModerateRisk')).toBe('bg-amber-100 text-amber-800');
    expect(component.getRiskBadgeClass('Healthy')).toBe('bg-emerald-100 text-emerald-800');
    expect(component.getRiskBadgeClass('Unknown')).toBe('bg-slate-100 text-slate-800');

    component.deals.set([]);
    expect(component.totalPipeline()).toBe(0);
    expect(component.forecastedRevenue()).toBe(0);
    expect(component.atRiskCount()).toBe(0);
    expect(component.aggregateHealth()).toBe(0);
  });

  it('should cap win probability at 95% when executing intervention on high probability deal', () => {
    const acmeDeal = component.deals().find(d => d.dealId === 'deal_acme_retail')!;
    expect(acmeDeal.winProbability).toBe(82);

    component.executeIntervention(acmeDeal);
    const updated = component.deals().find(d => d.dealId === 'deal_acme_retail')!;
    expect(updated.winProbability).toBe(95);
  });
});
