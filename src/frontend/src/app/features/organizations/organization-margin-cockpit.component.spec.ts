// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { TranslationService } from '../../core/services/translation.service';
import { OrganizationMarginCockpitComponent } from './organization-margin-cockpit.component';

describe('OrganizationMarginCockpitComponent', () => {
  let component: OrganizationMarginCockpitComponent;

  beforeEach(() => {
    const mockTranslation = {
      t: (key: string) => key,
      currentLanguage: signal('en'),
      setLanguage: vi.fn()
    };

    const injector = Injector.create({
      providers: [
        { provide: TranslationService, useValue: mockTranslation }
      ]
    });

    component = runInInjectionContext(injector, () => new OrganizationMarginCockpitComponent());
  });

  it('should initialize with organization tree hierarchy and pre-seeded deal ledger', () => {
    expect(component).toBeTruthy();
    expect(component.treeData().name).toBe('SGrowing Global Enterprise Tenant');
    expect(component.dealLedger().length).toBeGreaterThan(0);
    expect(component.consolidatedRevenue()).toBeGreaterThan(0);
    expect(component.consolidatedTrueMargin()).toBeGreaterThan(0);
  });

  it('should calculate true gross margin reactive formula (Revenue - COGS - Courier - Labor)', () => {
    component.calcRevenue.set(10000);
    component.calcCogs.set(2000);
    component.calcCourierFees.set(500);
    component.calcLaborHours.set(20);
    component.calcHourlyRate.set(25); // Labor cost = 500

    // Expected margin = 10000 - 2000 - 500 - 500 = 7000 (70%)
    expect(component.computedLaborCost()).toBe(500);
    expect(component.computedTrueMargin()).toBe(7000);
    expect(component.computedMarginPct()).toBe(70);
    expect(component.marginHealthLabel()).toContain('Healthy Unit Economics');
  });

  it('should record deal calculation and append to deal ledger', () => {
    const initialDealsCount = component.dealLedger().length;
    component.calcDealTitle.set('Test Enterprise Omnichannel Agreement');
    component.recordCurrentDealMargin();

    expect(component.dealLedger().length).toBe(initialDealsCount + 1);
    expect(component.dealLedger()[0].dealTitle).toBe('Test Enterprise Omnichannel Agreement');
    expect(component.toastMessage()).toContain('recorded with True Gross Margin');
  });

  it('should flag Low Margin Risk when true gross margin falls below 20%', () => {
    component.calcRevenue.set(1000);
    component.calcCogs.set(700);
    component.calcCourierFees.set(100);
    component.calcLaborHours.set(5);
    component.calcHourlyRate.set(25); // Labor = 125, margin = 1000 - 700 - 100 - 125 = 75 (7.5%)

    expect(component.computedMarginPct()).toBe(7.5);
    expect(component.marginHealthLabel()).toBe('Low Margin Risk (<20%)');
    expect(component.marginHealthColor()).toBe('text-red-400');
    expect(component.marginHealthBadge()).toContain('bg-red-500/20');
  });

  it('should flag Moderate Margin (20-40%) when true margin is between 20% and 40%', () => {
    component.calcRevenue.set(1000);
    component.calcCogs.set(500);
    component.calcCourierFees.set(100);
    component.calcLaborHours.set(4);
    component.calcHourlyRate.set(25); // Labor = 100, margin = 1000 - 500 - 100 - 100 = 300 (30%)

    expect(component.computedMarginPct()).toBe(30);
    expect(component.marginHealthLabel()).toBe('Moderate Margin (20-40%)');
    expect(component.marginHealthColor()).toBe('text-amber-400');
    expect(component.marginHealthBadge()).toContain('bg-amber-500/20');
  });
});
