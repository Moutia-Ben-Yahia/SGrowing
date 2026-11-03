// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { PltvAnalyticsStudioComponent } from './pltv-analytics-studio.component';

describe('PltvAnalyticsStudioComponent', () => {
  let component: PltvAnalyticsStudioComponent;

  beforeEach(() => {
    component = new PltvAnalyticsStudioComponent();
  });

  it('should initialize with pre-seeded customer pLTV cohort and computed tier metrics', () => {
    expect(component).toBeTruthy();
    expect(component.customers().length).toBeGreaterThanOrEqual(4);
    expect(component.championCount()).toBe(1);
    expect(component.atRiskCount()).toBe(1);
    expect(component.averageProbabilityAlive()).toBeGreaterThan(0);
    expect(component.totalPredicted365dLtv()).toBeGreaterThan(0);
  });

  it('should simulate latent BG/NBD transactions and Gamma-Gamma spend', () => {
    component.simFrequency = 5;
    component.simRecency = 180;
    component.simTotalDays = 200;
    component.simAov = 150;

    const pAlive = component.simPAlive();
    expect(pAlive).toBeGreaterThan(0);
    expect(pAlive).toBeLessThanOrEqual(100);

    const churnRisk = component.simChurnRisk();
    expect(churnRisk + pAlive).toBe(100);

    const expectedTx = component.simExpectedTx();
    expect(expectedTx).toBeGreaterThan(0);

    const expectedAov = component.simExpectedAov();
    expect(expectedAov).toBeGreaterThan(0);

    const pltv = component.simPltv365d();
    expect(pltv).toBeGreaterThan(0);
  });

  it('should queue intervention for an at-risk customer', () => {
    const atRiskCust = component.customers().find(c => c.tier === 'AtRiskDefecting')!;
    expect(atRiskCust.status).toBe('Active');

    component.queueIntervention(atRiskCust);

    const updated = component.customers().find(c => c.id === atRiskCust.id);
    expect(updated?.status).toBe('InterventionQueued');
  });

  it('should trigger bulk win-back for all customers requiring intervention', () => {
    component.triggerBulkWinBack();

    const requiringWinBack = component.customers().filter(c => c.requiresWinBack);
    expect(requiringWinBack.every(c => c.status === 'InterventionQueued')).toBe(true);
  });

  it('should return tier styling classes correctly', () => {
    expect(component.getTierClass('ChampionWhale')).toContain('emerald');
    expect(component.getTierClass('AtRiskDefecting')).toContain('rose');
    expect(component.getTierClass('SteadyLoyal')).toContain('cyan');
  });
});
