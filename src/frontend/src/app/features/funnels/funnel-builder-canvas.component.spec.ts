// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { FunnelBuilderCanvasComponent } from './funnel-builder-canvas.component';
import { AuthService } from '../../core/services/auth.service';

describe('FunnelBuilderCanvasComponent', () => {
  let component: FunnelBuilderCanvasComponent;
  let httpClientMock: any;
  let authServiceMock: any;

  beforeEach(() => {
    httpClientMock = {
      get: vi.fn().mockReturnValue(of({})),
      post: vi.fn().mockReturnValue(of({}))
    };

    authServiceMock = {
      currentTenant: signal({ id: 't-1', name: 'Demo Tenant' })
    };

    const injector = createEnvironmentInjector([
      { provide: HttpClient, useValue: httpClientMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new FunnelBuilderCanvasComponent();
    });
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('should create component with 15 steps and 9 declarative sections', () => {
    expect(component).toBeTruthy();
    expect(component.steps().length).toBe(15);
    expect(component.sections().length).toBe(9);
    expect(component.overallProgress()).toBe(100);
    expect(component.pricePoint()).toBe(49);
  });

  it('should calculate total preview price including tiered pricing and order bump', () => {
    // Pack 2 price multiplier is 1.70: 49 * 1.70 = 83.30 + bump 14.00 = 97.30
    component.selectedPack.set(2);
    component.isOrderBumpChecked.set(true);
    expect(component.totalPreviewPrice()).toBe('97.30');

    // Deselect order bump
    component.isOrderBumpChecked.set(false);
    expect(component.totalPreviewPrice()).toBe('83.30');

    // Pack 1: 49 * 1.0 = 49.00
    component.selectedPack.set(1);
    expect(component.totalPreviewPrice()).toBe('49.00');
  });

  it('should compute tier price and original tier price correctly', () => {
    // Pack 2: discounted = 49 * 1.70 = 83.30, original = 49 * 2 = 98.00
    expect(component.calculateTierPrice(2)).toBe('83.30');
    expect(component.calculateOriginalTierPrice(2)).toBe('98.00');
  });

  it('should apply presets for leather, charger, and serum', () => {
    component.applyPreset('charger');
    expect(component.productSku()).toBe('SKU-MAGFAST-02');
    expect(component.pricePoint()).toBe(39);

    component.applyPreset('serum');
    expect(component.productSku()).toBe('SKU-ARGAN-03');
    expect(component.pricePoint()).toBe(59);

    component.applyPreset('leather');
    expect(component.productSku()).toBe('SKU-LEATHER-01');
    expect(component.pricePoint()).toBe(49);
  });

  it('should publish funnel and display publish modal', () => {
    expect(component.isPublished()).toBe(false);
    expect(component.showPublishModal()).toBe(false);

    component.publishFunnel();

    expect(component.isPublished()).toBe(true);
    expect(component.showPublishModal()).toBe(true);
    expect(component.livePublishedUrl()).toContain('https://buy.sgrowing.io/f/');
  });

  it('should compile valid JSON section schema', () => {
    const json = component.compiledSchemaJson();
    expect(json).toContain('"name": "Artisan Handcrafted Full-Grain Leather Messenger Bag"');
    expect(json).toContain('"sku": "SKU-LEATHER-01"');
    expect(json).toContain('sec_order_bump');
  });

  it('should switch active tab and selected section', () => {
    component.activeTab.set('schema');
    expect(component.activeTab()).toBe('schema');

    component.selectedSectionId.set('sec_checkout_form');
    expect(component.selectedSection().id).toBe('sec_checkout_form');
  });
});
