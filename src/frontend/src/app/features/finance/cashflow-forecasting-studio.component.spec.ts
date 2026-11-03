// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { CashflowForecastingStudioComponent } from './cashflow-forecasting-studio.component';

describe('CashflowForecastingStudioComponent', () => {
  let component: CashflowForecastingStudioComponent;

  beforeEach(() => {
    component = new CashflowForecastingStudioComponent();
    component.ngOnInit();
  });

  it('should initialize with pre-seeded courier float and 90-day cash trajectory', () => {
    expect(component).toBeTruthy();
    expect(component.couriers().length).toBeGreaterThanOrEqual(3);
    expect(component.selectedHorizon()).toBe(90);
    const trajectory = component.displayedTrajectory();
    expect(trajectory.length).toBe(90);
    expect(component.deficitDaysCount()).toBeGreaterThanOrEqual(0);
    expect(component.lowestProjectedBalance()).toBeDefined();
  });

  it('should switch forecasting horizon between 30, 60, and 90 days', () => {
    component.selectedHorizon.set(30);
    expect(component.displayedTrajectory().length).toBe(30);

    component.selectedHorizon.set(60);
    expect(component.displayedTrajectory().length).toBe(60);
  });

  it('should simulate B2B freight courier invoice factoring to relieve float deficit', () => {
    const courier = component.couriers()[0];
    component.quickFactorCourier(courier);

    expect(component.openFactoringDrawer()).toBe(true);
    expect(component.factoringAdvanceInput()).toBeGreaterThan(0);

    component.applyFactoringSimulation();

    expect(component.factoredActive()).toBe(true);
    expect(component.openFactoringDrawer()).toBe(false);
    expect(component.factoredAmount()).toBeGreaterThan(0);

    component.resetFactoringSimulation();
    expect(component.factoredActive()).toBe(false);
    expect(component.factoredAmount()).toBe(0);
  });

  it('should add manual cashflow adjustments and recalculate trajectory', () => {
    const adjustmentsBefore = component.manualAdjustments().length;
    component.adjustmentDayOffset = 10;
    component.adjustmentAmount = 15000;
    component.adjustmentIsPositive.set(true);
    component.adjustmentDescription = 'Grant Subsidy Received';

    component.commitAdjustment();

    expect(component.manualAdjustments().length).toBe(adjustmentsBefore + 1);
    expect(component.openAdjustmentModal()).toBe(false);
  });

  it('should calculate bar height percent and buffer line percent safely', () => {
    const barPct = component.calculateBarHeightPercent(50000);
    expect(barPct).toBe(50);

    const bufferPct = component.calculateBufferLinePercent();
    expect(bufferPct).toBeGreaterThan(0);
    expect(bufferPct).toBeLessThanOrEqual(100);
  });
});
