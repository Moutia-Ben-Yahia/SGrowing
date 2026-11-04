// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { AdSpendAutopilotCockpitComponent } from './ad-spend-autopilot-cockpit.component';

describe('AdSpendAutopilotCockpitComponent', () => {
  let component: AdSpendAutopilotCockpitComponent;

  beforeEach(() => {
    component = new AdSpendAutopilotCockpitComponent();
  });

  it('should create component and render initial managed campaigns', () => {
    expect(component).toBeTruthy();
    expect(component.campaigns().length).toBeGreaterThanOrEqual(3);
    expect(component.anomalyCount()).toBeGreaterThanOrEqual(1);
    expect(component.totalDailyBudget()).toBeGreaterThan(0);
  });

  it('should execute marginal ROAS budget rebalance', () => {
    const rec = component.recommendation();
    expect(rec).toBeTruthy();

    if (rec) {
      component.executeRebalance(rec);
      const google = component.campaigns().find(c => c.campaignId === 'cmp_google_pmax');
      const meta = component.campaigns().find(c => c.campaignId === 'cmp_meta_retargeting');

      expect(google?.dailyBudget).toBe(4000); // 3500 + 500
      expect(meta?.dailyBudget).toBe(1700); // 2200 - 500
      expect(component.recommendation()).toBeNull();
    }
  });

  it('should simulate bid shading and update savings telemetry', () => {
    const initialSavings = component.bidShadingSavings();
    component.simulateBidShading();

    expect(component.bidShadingSavings()).toBeGreaterThan(initialSavings);
    expect(component.simulatedShadedBid).toBeLessThan(component.simulatedRawBid);
  });

  it('should allow campaign selection and return correct status badge classes', () => {
    const metaCamp = component.campaigns()[1];
    component.selectCampaign(metaCamp);
    expect(component.selectedCampaign()?.campaignId).toBe('cmp_meta_retargeting');
    expect(component.getStatusBadgeClass('Optimized')).toContain('emerald');
    expect(component.getStatusBadgeClass('AnomalyPaused')).toContain('rose');
  });
});
