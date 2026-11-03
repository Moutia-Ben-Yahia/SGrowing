// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { DynamicPricingStudioComponent } from './dynamic-pricing-studio.component';

describe('DynamicPricingStudioComponent (Multi-Armed Bandit & Price Elasticity)', () => {
  let component: DynamicPricingStudioComponent;
  let mockHttp: any;

  const mockExperiments = [
    {
      sku: 'SKU-SAAS-ENTERPRISE-ADDON',
      productName: 'Enterprise AI Agent Workforce Addon',
      category: 'SaaS',
      baseCostCogs: 10.0,
      shippingAllocated: 0.0,
      minimumMarginHurdle: 0.5,
      standardPrice: 49.0,
      currentStock: 100,
      dailyRunRate: 5.0,
      maxMarkdownPct: 0.3,
      markdownStep: 0.05,
      hardMarginFloor: 15.0,
      daysOfSupply: 20,
      arms: [
        {
          armId: 'arm-1',
          price: 39.0,
          alpha: 12,
          beta: 45,
          impressions: 57,
          conversions: 12,
          totalRevenue: 468.0,
          conversionRate: 0.21,
          expectedValue: 8.21
        },
        {
          armId: 'arm-2',
          price: 49.0,
          alpha: 8,
          beta: 52,
          impressions: 60,
          conversions: 8,
          totalRevenue: 392.0,
          conversionRate: 0.13,
          expectedValue: 6.53
        }
      ],
      createdAtUtc: new Date().toISOString(),
      lastUpdatedUtc: new Date().toISOString()
    }
  ];

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/bandit')) {
          return of(mockExperiments);
        }
        return of([]);
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/select-arm')) {
          return of({
            sku: body?.sku || 'SKU-SAAS-ENTERPRISE-ADDON',
            selectedArmId: 'arm-1',
            proposedPrice: 39.0,
            finalPrice: 39.0,
            floorClampApplied: false,
            floorPrice: 15.0,
            sampledTheta: 0.22,
            sampledExpectedRevenue: 8.58,
            allSampledThetas: { 'arm-1': 0.22, 'arm-2': 0.14 },
            timestampUtc: new Date().toISOString()
          });
        }
        if (url.includes('/record-conversion')) {
          return of({
            ...mockExperiments[0],
            arms: [
              { ...mockExperiments[0].arms[0], conversions: 13, impressions: 58 }
            ]
          });
        }
        if (url.includes('/evaluate-guardrail')) {
          return of({
            sku: body?.sku,
            candidatePrice: body?.candidatePrice,
            floorPrice: 15.0,
            baseCostCogs: 10.0,
            minimumMarginHurdle: 0.5,
            shippingAllocated: 0.0,
            effectiveMarginPercent: 61.5,
            floorClampApplied: false,
            effectivePrice: body?.candidatePrice,
            guardrailStatus: 'Approved'
          });
        }
        if (url.includes('/markdown-schedule')) {
          return of([
            {
              sku: 'SKU-SAAS-ENTERPRISE-ADDON',
              productName: 'Enterprise AI Agent Workforce Addon',
              currentStock: 100,
              dailyRunRate: 5.0,
              daysOfSupply: 20,
              standardPrice: 49.0,
              hardMarginFloor: 15.0,
              recommendedMarkdownPct: 0.0,
              rawDiscountedPrice: 49.0,
              finalPrice: 49.0,
              liquidationTriggered: false,
              marginFloorClamped: false,
              liquidationTier: 'Healthy'
            }
          ]);
        }
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new DynamicPricingStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with experiments and liquidation schedules', () => {
    expect(component).toBeTruthy();
    expect(component.experiments().length).toBe(1);
    expect(component.currentExperiment()).not.toBeNull();
    expect(component.currentExperiment()?.sku).toBe('SKU-SAAS-ENTERPRISE-ADDON');
    expect(component.activeTab()).toBe('bandit');
  });

  it('should switch tabs between bandit, elasticity, guardrails, and liquidation', () => {
    component.activeTab.set('elasticity');
    expect(component.activeTab()).toBe('elasticity');

    component.activeTab.set('guardrails');
    expect(component.activeTab()).toBe('guardrails');

    component.activeTab.set('liquidation');
    expect(component.activeTab()).toBe('liquidation');
  });

  it('should simulate Thompson sampling arm selection', () => {
    component.simulateSession();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.simulationResult()).not.toBeNull();
    expect(component.simulationResult()?.selectedArmId).toBe('arm-1');
    expect(component.simulationResult()?.finalPrice).toBe(39.0);
  });

  it('should record conversion sale and update conversion banner', () => {
    component.simulateSession();
    component.recordSimulationSale();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.conversionSuccessMessage()).toContain('Converted sale at $39.00');
  });

  it('should evaluate margin floor guardrail against candidate price', () => {
    component.candidatePriceInput = 12.0; // Below 15.0 floor
    component.evaluateGuardrail();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.guardrailCheck()).not.toBeNull();
    expect(component.guardrailCheck()?.floorPrice).toBe(15.0);
  });
});
