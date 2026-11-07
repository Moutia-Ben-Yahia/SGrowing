// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { RevenueForecastSimulationStudioComponent } from './revenue-forecast-simulation-studio.component';

describe('RevenueForecastSimulationStudioComponent', () => {
  let component: RevenueForecastSimulationStudioComponent;

  beforeEach(() => {
    component = new RevenueForecastSimulationStudioComponent();
  });

  it('should initialize component with default enterprise opportunities and metrics', () => {
    expect(component).toBeTruthy();
    expect(component.opportunities().length).toBe(10);
    expect(component.totalPipeline()).toBe(3560000);
    expect(component.activeTab()).toBe('simulation');
    expect(component.quotaTarget()).toBe(1800000);
  });

  it('should compute valid Monte Carlo P10, P50, and P90 percentiles with logical ordering', () => {
    const p10 = component.p10Value();
    const p50 = component.p50Value();
    const p90 = component.p90Value();

    expect(p10).toBeGreaterThan(0);
    expect(p50).toBeGreaterThanOrEqual(p10);
    expect(p90).toBeGreaterThanOrEqual(p50);
    expect(component.quotaAttainmentPct()).toBeGreaterThanOrEqual(0);
    expect(component.quotaAttainmentPct()).toBeLessThanOrEqual(100);
  });

  it('should re-run simulation when runSimulation is triggered and update simulationResult', () => {
    component.iterations.set(1000);
    component.runSimulation();

    const result = component.simulationResult();
    expect(result).not.toBeNull();
    expect(result?.iterations).toBe(1000);
    expect(result?.histogramBins.length).toBe(10);
    expect(result?.percentileDistribution.length).toBe(10);
  });

  it('should filter opportunities by pipeline stage', () => {
    expect(component.filteredOpportunities().length).toBe(10);

    component.selectedStageFilter.set('ContractNegotiation');
    expect(component.filteredOpportunities().length).toBe(3);
    expect(component.filteredOpportunities().every(o => o.stage === 'ContractNegotiation')).toBe(true);

    component.selectedStageFilter.set('All');
    expect(component.filteredOpportunities().length).toBe(10);
  });

  it('should calculate critical slippage alerts and exposure value', () => {
    const alerts = component.slippageAlerts();
    expect(alerts.length).toBe(4);
    expect(component.criticalAlertsCount()).toBeGreaterThan(0);
    expect(component.criticalSlippageValue()).toBeGreaterThan(0);

    const critAlert = alerts.find(a => a.riskLevel === 'Critical');
    expect(critAlert).toBeDefined();
    expect(critAlert?.recommendedIntervention).toBeTruthy();
  });

  it('should update interactive scenario runner parameters (quota target, iterations, distribution, volatility)', () => {
    component.quotaTarget.set(2000000);
    component.iterations.set(10000);
    component.distributionModel.set('LogNormal');
    component.marketVolatilityIndex.set(1.25);

    expect(component.quotaTarget()).toBe(2000000);
    expect(component.iterations()).toBe(10000);
    expect(component.distributionModel()).toBe('LogNormal');
    expect(component.marketVolatilityIndex()).toBe(1.25);

    component.runSimulation();
    expect(component.simulationResult()?.targetQuota).toBe(2000000);
  });

  it('should add new opportunity through modal submission and include in simulation', () => {
    component.openAddModal();
    expect(component.showAddModal()).toBe(true);

    component.newDealName = 'Gulf Bank Cyber Security Integration';
    component.newAccountName = 'Gulf National Bank';
    component.newAmount = 500000;
    component.newWinProb = 0.8;
    component.newStage = 'ContractNegotiation';
    component.newCategory = 'Commit';

    component.submitAddOpportunity();

    expect(component.showAddModal()).toBe(false);
    expect(component.opportunities().length).toBe(11);
    expect(component.totalPipeline()).toBe(4060000);
    expect(component.opportunities().some(o => o.dealName === 'Gulf Bank Cyber Security Integration')).toBe(true);
  });

  it('should calculate histogram bar height and spectrum percentages within boundaries', () => {
    const h1 = component.getHistogramBarHeight(0);
    const h2 = component.getHistogramBarHeight(15);
    const h3 = component.getHistogramBarHeight(40);

    expect(h1).toBeGreaterThanOrEqual(8);
    expect(h2).toBeGreaterThan(h1);
    expect(h3).toBeLessThanOrEqual(100);

    const startPct = component.spectrumStartPercent();
    const widthPct = component.spectrumWidthPercent();

    expect(startPct).toBeGreaterThanOrEqual(5);
    expect(startPct).toBeLessThanOrEqual(80);
    expect(widthPct).toBeGreaterThanOrEqual(15);
  });

  it('should switch between tabs smoothly', () => {
    component.activeTab.set('scenario-runner');
    expect(component.activeTab()).toBe('scenario-runner');

    component.activeTab.set('slippage-radar');
    expect(component.activeTab()).toBe('slippage-radar');

    component.activeTab.set('pipeline');
    expect(component.activeTab()).toBe('pipeline');

    component.activeTab.set('simulation');
    expect(component.activeTab()).toBe('simulation');
  });
});
