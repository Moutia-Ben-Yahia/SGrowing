// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { of } from 'rxjs';
import { createEnvironmentInjector, signal } from '@angular/core';
import { BillingStudioComponent } from './billing-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';

describe('BillingStudioComponent', () => {
  let component: BillingStudioComponent;
  let mockApi: any;
  let mockAuth: any;

  beforeEach(() => {
    vi.spyOn(window, 'alert').mockImplementation(() => {});

    mockApi = {
      getSubscriptionDetails: vi.fn().mockReturnValue(of({
        planTier: 2,
        status: 'Active',
        monthlyPrice: 249,
        billingCycleEndUtc: new Date().toISOString(),
        quotaAiTokens: 500000,
        usedAiTokens: 120000,
        quotaVoiceMinutes: 1000,
        usedVoiceMinutes: 240,
        quotaMessages: 50000,
        usedMessages: 15400
      })),
      getInvoices: vi.fn().mockReturnValue(of([
        {
          id: 'inv-1',
          invoiceNumber: 'INV-2026-001',
          amountDue: 249,
          status: 'Paid',
          createdAtUtc: new Date().toISOString()
        }
      ])),
      upgradePlan: vi.fn().mockReturnValue(of({ success: true })),
      createCheckoutSession: vi.fn().mockReturnValue(of({
        checkoutUrl: 'https://checkout.stripe.com/pay/cs_test_123'
      }))
    };

    mockAuth = {
      currentTenant: signal({ id: 'tenant-test-123', name: 'Acme Corp' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: mockApi },
      { provide: AuthService, useValue: mockAuth }
    ]);

    injector.runInContext(() => {
      component = new BillingStudioComponent();
    });
  });

  it('should create component and resolve tenantId from auth service', () => {
    expect(component).toBeTruthy();
    expect(component.tenantId).toBe('tenant-test-123');
  });

  it('should load subscription details and invoices on initialization', () => {
    component.ngOnInit();
    expect(mockApi.getSubscriptionDetails).toHaveBeenCalledWith('tenant-test-123');
    expect(mockApi.getInvoices).toHaveBeenCalledWith('tenant-test-123');

    const sub = component.sub();
    expect(sub).toBeTruthy();
    expect(sub?.planTier).toBe(2);
    expect(sub?.monthlyPrice).toBe(249);

    const invoices = component.invoices();
    expect(invoices.length).toBe(1);
    expect(invoices[0].invoiceNumber).toBe('INV-2026-001');
  });

  it('should trigger upgradePlan and reload refreshed subscription state', () => {
    component.upgrade(3);
    expect(mockApi.upgradePlan).toHaveBeenCalledWith({
      tenantId: 'tenant-test-123',
      targetTier: 3
    });
    expect(mockApi.getSubscriptionDetails).toHaveBeenCalled();
  });

  it('should initiate Stripe checkout portal session', () => {
    const alertSpy = vi.spyOn(window, 'alert');
    component.openStripePortal();

    expect(mockApi.createCheckoutSession).toHaveBeenCalledWith(expect.objectContaining({
      tenantId: 'tenant-test-123',
      targetTier: 3
    }));
    expect(alertSpy).toHaveBeenCalledWith(expect.stringContaining('Stripe Checkout Session initialized'));
  });

  it('should fallback to default tenant ID when auth service returns null tenant', () => {
    mockAuth.currentTenant.set(null);
    expect(component.tenantId).toBe('00000000-0000-0000-0000-000000000001');
  });
});
