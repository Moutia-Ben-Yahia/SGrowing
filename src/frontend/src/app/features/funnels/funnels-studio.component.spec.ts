// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { of } from 'rxjs';
import { FunnelsStudioComponent } from './funnels-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SalesFunnelDto } from '../../core/models/funnel.models';

describe('FunnelsStudioComponent', () => {
  let component: FunnelsStudioComponent;
  let apiServiceMock: any;
  let authServiceMock: any;

  const mockFunnel: SalesFunnelDto = {
    id: 'f-001',
    tenantId: 't-1',
    name: 'Sneakers Launch Funnel',
    slug: 'sneakers-launch',
    description: 'Main flagship funnel',
    currency: 'TND',
    isActive: true,
    totalVisitors: 1200,
    totalOrders: 60,
    totalRevenue: 5400,
    steps: []
  };

  beforeEach(() => {
    apiServiceMock = {
      getFunnels: vi.fn().mockReturnValue(of([mockFunnel])),
      getFunnelSteps: vi.fn().mockReturnValue(of([])),
      getOrderBumps: vi.fn().mockReturnValue(of([])),
      getOneClickUpsells: vi.fn().mockReturnValue(of([])),
      getFunnelPerformance: vi.fn().mockReturnValue(of({
        funnelId: 'f-001',
        totalVisitors: 1500,
        totalOrders: 75,
        totalRevenue: 6750,
        conversionRatePercentage: 5.0,
        bumpRevenue: 300,
        upsellRevenue: 600
      })),
      recordFunnelEvent: vi.fn().mockReturnValue(of({ success: true })),
      createFunnel: vi.fn().mockReturnValue(of(mockFunnel)),
      addFunnelStep: vi.fn().mockReturnValue(of({ id: 'step-1' })),
      createOrderBump: vi.fn().mockReturnValue(of({ id: 'bump-1' })),
      createOneClickUpsell: vi.fn().mockReturnValue(of({ id: 'upsell-1' }))
    };

    authServiceMock = {
      currentTenant: signal({ id: 't-1', name: 'Demo Tenant' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new FunnelsStudioComponent();
    });
  });

  it('should initialize and load funnels on init', () => {
    component.ngOnInit();
    expect(component).toBeTruthy();
    expect(apiServiceMock.getFunnels).toHaveBeenCalledWith('t-1');
    expect(component.funnels().length).toBe(1);
    expect(component.activeFunnel()?.name).toBe('Sneakers Launch Funnel');
  });

  it('should select funnel and fetch steps, bumps, upsells and performance', () => {
    component.selectFunnel(mockFunnel);

    expect(apiServiceMock.getFunnelSteps).toHaveBeenCalledWith('f-001', 't-1');
    expect(apiServiceMock.getOrderBumps).toHaveBeenCalledWith('f-001', 't-1');
    expect(apiServiceMock.getOneClickUpsells).toHaveBeenCalledWith('f-001', 't-1');
    expect(apiServiceMock.getFunnelPerformance).toHaveBeenCalledWith('f-001', 't-1');
    expect(component.performance()?.totalOrders).toBe(75);
  });

  it('should open upsell simulator and clear previous success message', () => {
    component.upsellSuccessMessage.set('Previous success');
    component.openUpsellSimulator();

    expect(component.upsellSuccessMessage()).toBeNull();
    expect(component.showUpsellSimulator()).toBe(true);
  });

  it('should accept upsell and record event', () => {
    component.activeFunnel.set(mockFunnel);
    component.acceptUpsell();

    expect(apiServiceMock.recordFunnelEvent).toHaveBeenCalledWith('f-001', expect.objectContaining({
      tenantId: 't-1',
      eventType: 4,
      value: 39.0
    }));
    expect(component.upsellSuccessMessage()).toContain('1-Click Upsell added');
  });

  it('should toggle create funnel modal', () => {
    expect(component.showCreateModal()).toBe(false);
    component.showCreateModal.set(true);
    expect(component.showCreateModal()).toBe(true);
  });
});
