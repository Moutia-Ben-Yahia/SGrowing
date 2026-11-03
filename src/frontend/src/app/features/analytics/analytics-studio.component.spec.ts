// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AnalyticsStudioComponent } from './analytics-studio.component';

describe('AnalyticsStudioComponent', () => {
  let component: AnalyticsStudioComponent;

  beforeEach(() => {
    vi.spyOn(window, 'alert').mockImplementation(() => {});
    component = new AnalyticsStudioComponent();
  });

  it('should initialize with default model, channels, ad sets and creator stats', () => {
    expect(component).toBeTruthy();
    expect(component.selectedModel).toBe('WShaped');
    expect(component.channels().length).toBeGreaterThan(0);
    expect(component.creators().length).toBe(2);
    expect(component.adSets().length).toBe(3);
    expect(component.auditLogs().length).toBe(3);
    expect(component.totalRevenue()).toBe(125000);
  });

  it('should switch attribution models and recalculate touchpoint weights', () => {
    component.selectedModel = 'FirstTouch';
    component.applyModelChange();
    const firstTouch = component.channels();
    const firstItem = firstTouch.find(c => c.stage === 'First Discovery');
    expect(firstItem?.weight).toBe(100);

    component.selectedModel = 'LastTouch';
    component.applyModelChange();
    const lastTouch = component.channels();
    const lastItem = lastTouch.find(c => c.stage === 'Deal Won');
    expect(lastItem?.weight).toBe(100);

    component.selectedModel = 'WShaped';
    component.applyModelChange();
    const wShaped = component.channels();
    expect(wShaped.some(c => c.weight === 40)).toBe(true);
  });

  it('should trigger ad spend optimizer simulation and alert real cash ROAS', () => {
    const alertSpy = vi.spyOn(window, 'alert');
    component.triggerOptimizerRun();
    expect(alertSpy).toHaveBeenCalledWith(expect.stringContaining('ROAS'));
  });

  it('should simulate affiliate escrow payout batch', () => {
    const alertSpy = vi.spyOn(window, 'alert');
    component.simulatePayoutBatch();
    expect(alertSpy).toHaveBeenCalledWith(expect.stringContaining('Affiliate Escrow Payout'));
  });
});
