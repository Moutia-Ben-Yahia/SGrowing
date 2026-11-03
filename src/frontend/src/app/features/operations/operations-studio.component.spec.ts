// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { OperationsStudioComponent } from './operations-studio.component';

describe('OperationsStudioComponent (Core Operations Studio)', () => {
  let component: OperationsStudioComponent;

  beforeEach(() => {
    vi.spyOn(window, 'alert').mockImplementation(() => {});
    component = new OperationsStudioComponent();
  });

  it('should initialize with confirmation tab and audio alerts enabled', () => {
    expect(component.activeTab()).toBe('confirmation');
    expect(component.audioAlertsEnabled()).toBe(true);
    expect(component.orders().length).toBeGreaterThan(0);
    expect(component.pendingCount()).toBeGreaterThan(0);
  });

  it('should support tab navigation across domains', () => {
    component.activeTab.set('logistics');
    expect(component.activeTab()).toBe('logistics');

    component.activeTab.set('crossborder');
    expect(component.activeTab()).toBe('crossborder');

    component.activeTab.set('fleet');
    expect(component.activeTab()).toBe('fleet');

    component.activeTab.set('wms');
    expect(component.activeTab()).toBe('wms');
  });

  it('should manage Multi-Currency FX Rates & Regional Pricing (Tab 14)', () => {
    component.activeTab.set('crossborder');
    const countries = component.tradeCountries();
    const fx = component.fxRates();

    expect(countries.some(c => c.code === 'TN' && c.currency === 'TND')).toBe(true);
    expect(countries.some(c => c.code === 'DZ' && c.currency === 'DZD')).toBe(true);
    expect(countries.some(c => c.code === 'SA' && c.currency === 'SAR')).toBe(true);

    expect(fx.some(rate => rate.pair === 'USD/TND')).toBe(true);
    expect(fx.some(rate => rate.pair === 'EUR/TND')).toBe(true);

    const alertSpy = vi.spyOn(window, 'alert');
    component.refreshFxRatesUi();
    expect(alertSpy).toHaveBeenCalledWith(expect.stringContaining('Live FX exchange rates refreshed'));
  });

  it('should manage 3PL Courier Connectors & Webhook Telemetry (Tab 16)', () => {
    component.activeTab.set('fleet');
    const carriers = component.courierConnectors();

    expect(carriers.length).toBe(4);
    expect(carriers.some(c => c.name === 'Yalidine Express' && c.status === 'Connected')).toBe(true);
    expect(carriers.some(c => c.name === 'Aramex Tunisia' && c.status === 'Connected')).toBe(true);
    expect(carriers.some(c => c.name === 'Bosta Fulfillment')).toBe(true);
    expect(carriers.some(c => c.name === 'Mylerz Same-Day')).toBe(true);

    const alertSpy = vi.spyOn(window, 'alert');
    component.syncAllCarrierWebhooksUi();
    expect(alertSpy).toHaveBeenCalledWith(expect.stringContaining('3,360 tracking events synchronized'));

    component.testCarrierPingUi(carriers[0]);
    expect(alertSpy).toHaveBeenCalledWith(expect.stringContaining('Carrier API ping successful'));

    component.reconcileCarrierCashUi(carriers[0]);
    expect(alertSpy).toHaveBeenCalledWith(expect.stringContaining('Cash Reconciliation completed'));
  });

  it('should handle order confirmation anti-collision lock and status advancement', () => {
    const order = component.orders()[0];
    expect(order).toBeDefined();

    component.acquireLock(order);
    const lockedOrder = component.orders().find(o => o.id === order.id);
    expect(lockedOrder?.isLockedByMe).toBe(true);

    component.confirmOrder(order);
    const confirmedOrder = component.orders().find(o => o.id === order.id);
    expect(confirmedOrder?.status).toBe('Confirmed');
  });

  it('should support barcode pick slip verification and WMS fulfillment dispatch', () => {
    const pickSlip = component.activePickSlip();
    expect(pickSlip.items.length).toBe(2);

    component.handleBarcodeScan('6191234567890');
    expect(component.pickedCount()).toBe(1);
    expect(component.scanFeedback()?.success).toBe(true);
    expect(pickSlip.items[0].isVerified).toBe(true);

    component.completeFulfillmentPack();
    const wms = component.wmsStock();
    expect(wms.some(item => item.inTransit > 0)).toBe(true);
  });
});
