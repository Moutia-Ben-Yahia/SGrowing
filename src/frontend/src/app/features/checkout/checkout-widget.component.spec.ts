// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { CheckoutWidgetComponent } from './checkout-widget.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { of } from 'rxjs';

describe('CheckoutWidgetComponent', () => {
  let component: CheckoutWidgetComponent;
  let apiServiceMock: any;
  let authServiceMock: any;

  const mockWilayas = [
    {
      id: 'w-tun',
      code: 'TUN',
      nameFr: 'Tunis',
      nameAr: 'تونس',
      countryCode: 'TN',
      standardShippingFee: 7.0,
      estimatedDeliveryDays: 1,
      delegationsJson: JSON.stringify([{ name: 'La Marsa' }, { name: 'Carthage' }])
    },
    {
      id: 'w-sfx',
      code: 'SFX',
      nameFr: 'Sfax',
      nameAr: 'صفاقس',
      countryCode: 'TN',
      standardShippingFee: 9.0,
      estimatedDeliveryDays: 2,
      delegationsJson: JSON.stringify([{ name: 'Sfax Ville' }])
    }
  ];

  beforeEach(() => {
    apiServiceMock = {
      getCheckoutWilayas: vi.fn().mockReturnValue(of(mockWilayas)),
      sendCheckoutOtp: vi.fn().mockReturnValue(of({ success: true, simulatedOtp: '8492' }))
    };

    authServiceMock = {
      currentTenant: signal({ id: 'tenant-cod-123', name: 'COD Store' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new CheckoutWidgetComponent();
    });

    component.ngOnInit();
  });

  it('should initialize with loaded wilayas, default customer, and active shipping fee', () => {
    expect(component).toBeTruthy();
    expect(apiServiceMock.getCheckoutWilayas).toHaveBeenCalledWith('TN');
    expect(component.wilayas().length).toBe(2);
    expect(component.shippingFee()).toBe(7.0);
    expect(component.delegations().length).toBeGreaterThan(0);
  });

  it('should update shipping fee and delegations when switching wilaya', () => {
    component.onWilayaChange('SFX');

    expect(component.selectedWilayaCode()).toBe('SFX');
    expect(component.shippingFee()).toBe(9.0);
    expect(component.currentWilaya()?.nameFr).toBe('Sfax');
    expect(component.delegations().length).toBe(1);
    expect(component.selectedDelegation).toBe('Sfax Ville');
  });

  it('should handle simulated phone OTP verification cycle', () => {
    component.customerPhone = '+21698123456';
    component.triggerSendOtp();

    expect(apiServiceMock.sendCheckoutOtp).toHaveBeenCalled();
    expect(component.otpSimulatedCode()).toBe('8492');

    component.enteredOtp = '8492';
    component.triggerVerifyOtp();

    expect(component.isOtpVerified()).toBe(true);
    expect(component.otpSimulatedCode()).toBeNull();
  });

  it('should accept upsell and recompute final total amount dynamically', () => {
    const initialTotal = component.finalTotalAmount();

    component.acceptUpsell();
    expect(component.upsellAccepted()).toBe(true);
    expect(component.finalTotalAmount()).toBe(initialTotal + component.upsellAmount());
  });

  it('should submit COD order session and record confirmed order number', () => {
    component.customerName = 'Ahmed Mansour';
    component.customerPhone = '+21698765432';
    component.streetAddress = 'Avenue Habib Bourguiba, Tunis';

    component.submitCodOrder();

    expect(component.orderCompleted()).toBe(true);
    expect(component.confirmedOrderNumber()).toMatch(/^ORD-\d+$/);
  });
});
