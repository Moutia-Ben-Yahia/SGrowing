// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { WhatsAppPairingCockpitComponent } from './whatsapp-pairing-cockpit.component';

describe('WhatsAppPairingCockpitComponent', () => {
  let component: WhatsAppPairingCockpitComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue(of({})),
      post: vi.fn().mockReturnValue(of({
        pairingCode: 'M9X4-L2KT',
        expiresInSeconds: 600
      }))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new WhatsAppPairingCockpitComponent());
    component.ngOnInit();
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('should initialize with country presets, default pairing code, and simulated orders', () => {
    expect(component).toBeTruthy();
    expect(component.countryPresets.length).toBeGreaterThanOrEqual(6);
    expect(component.selectedCountry()).toBe('TN');
    expect(component.simulatedOrders().length).toBeGreaterThanOrEqual(3);
    expect(component.connectionStatus()).toBe('PendingEntry');
  });

  it('should switch regional presets and update dial code prefix', () => {
    const egPreset = component.countryPresets.find(p => p.code === 'EG')!;
    component.selectPreset(egPreset);

    expect(component.selectedCountry()).toBe('EG');
    expect(component.phoneNumberInput).toBe('+20');
  });

  it('should request pairing code via HTTP and start countdown', () => {
    component.phoneNumberInput = '+21698123456';
    component.requestPairingCode();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/channels/whatsapp/pairing/request',
      expect.objectContaining({ phoneNumber: '+21698123456' })
    );
    expect(component.pairingCode()).toBe('M9X4-L2KT');
    expect(component.connectionStatus()).toBe('PendingEntry');
    expect(component.remainingSeconds()).toBe(600);
  });

  it('should handle simulated customer interactive button confirmation', () => {
    const activeOrder = component.activeSimulatedOrder();
    expect(activeOrder).toBeTruthy();

    component.onSimulatedButtonClick('btn_confirm');

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/channels/whatsapp/interactive/webhook',
      expect.objectContaining({ orderId: activeOrder.id, buttonId: 'btn_confirm' })
    );

    const updated = component.simulatedOrders().find(o => o.id === activeOrder.id);
    expect(updated?.status).toBe('Confirmed');
    expect(component.simulatorFeedback()).toContain('Order confirmed');
  });

  it('should handle order cancellation and update reservation status', () => {
    const activeOrder = component.activeSimulatedOrder();
    component.onSimulatedButtonClick('btn_cancel');

    const updated = component.simulatedOrders().find(o => o.id === activeOrder.id);
    expect(updated?.status).toBe('Cancelled');
    expect(updated?.reservationStatus).toBe('Released');
    expect(component.simulatorFeedback()).toContain('Order cancelled');
  });

  it('should disconnect device session cleanly', () => {
    component.disconnectInstance();
    expect(component.connectionStatus()).toBe('Disconnected');
    expect(component.pairingCode()).toBe('');
  });
});
