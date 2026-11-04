// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { BiReportBuilderComponent } from './bi-report-builder.component';

describe('BiReportBuilderComponent', () => {
  let component: BiReportBuilderComponent;

  beforeEach(() => {
    component = new BiReportBuilderComponent();
  });

  it('should initialize with Monte Carlo scenario distribution and base ARR', () => {
    expect(component).toBeTruthy();
    expect(component.baseArr()).toBe(10_000_000);
    expect(component.scenarios().length).toBe(5);
    expect(component.savedReports().length).toBe(2);
  });

  it('should toggle between Monte Carlo and Pivot Builder tabs', () => {
    expect(component.activeTab()).toBe('monte-carlo');

    component.activeTab.set('pivot-builder');
    expect(component.activeTab()).toBe('pivot-builder');
    expect(component.pivotRows().length).toBe(3);
  });

  it('should recalculate Monte Carlo distribution when hyperparameters change', () => {
    component.growthMean = 40;
    component.targetArrInput = 14_000_000;
    component.runMonteCarloSimulation();

    expect(component.simP50Arr()).toBe(14_000_000);
    expect(component.simP90Arr()).toBe(Math.round(14_000_000 * 1.2));
    expect(component.probTarget()).toBe(78.5);
  });

  it('should lower probability target when target ARR exceeds P50 projection', () => {
    component.growthMean = 10;
    component.targetArrInput = 13_000_000;
    component.runMonteCarloSimulation();

    expect(component.simP50Arr()).toBe(11_000_000);
    expect(component.probTarget()).toBe(45.2);
    expect(component.targetArr()).toBe(13_000_000);
  });

  it('should allow modifying pivotRows and savedReports signals', () => {
    component.pivotRows.update(rows => [
      ...rows,
      { dimension: 'Organic Search', subDimension: '2026-Q3', metricValue: 1200000, secondaryValue: 95.0, formattedMetric: '$1.20M' }
    ]);
    expect(component.pivotRows().length).toBe(4);
    expect(component.pivotRows()[3].dimension).toBe('Organic Search');
  });
});
