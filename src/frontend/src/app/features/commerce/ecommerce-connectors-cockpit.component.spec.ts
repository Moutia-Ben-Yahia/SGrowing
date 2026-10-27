// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EcommerceConnectorsCockpitComponent } from './ecommerce-connectors-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('EcommerceConnectorsCockpitComponent (Frontier 4 Cockpit)', () => {
  let translation: TranslationService;
  let component: EcommerceConnectorsCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    translation = new TranslationService();
    translation.setLocale('en-US');
    component = new EcommerceConnectorsCockpitComponent(translation);
  });

  it('should initialize with default Shopify and WooCommerce configurations', () => {
    const sfy = component.shopifyConfig();
    const wc = component.wooConfig();

    expect(sfy.platform).toBe('Shopify');
    expect(sfy.storeDomain).toBe('sgrowing-flagship.myshopify.com');
    expect(sfy.isActive).toBe(true);

    expect(wc.platform).toBe('WooCommerce');
    expect(wc.storeDomain).toBe('shop.sgrowing-boutique.com');
    expect(wc.isActive).toBe(true);

    // Initial computed totals
    expect(component.totalSyncedOrders()).toBe(25); // 14 + 11
    expect(component.totalActiveDeals()).toBe(25);
    expect(component.totalShipments()).toBe(25);
    expect(component.totalCatalogProducts()).toBe(6); // 3 + 3
  });

  it('should simulate an inbound Shopify order and reactively update metrics and feed', () => {
    const initialOrders = component.totalSyncedOrders();
    const initialFeedLength = component.ingestionEvents().length;

    component.simulateInboundOrder('Shopify');

    expect(component.totalSyncedOrders()).toBe(initialOrders + 1);
    expect(component.totalActiveDeals()).toBe(initialOrders + 1);
    expect(component.totalShipments()).toBe(initialOrders + 1);
    expect(component.ingestionEvents().length).toBe(initialFeedLength + 1);

    const latestEvent = component.ingestionEvents()[0];
    expect(latestEvent.platform).toBe('Shopify');
    expect(latestEvent.orderNumber).toMatch(/^#SFY-\d+$/);
    expect(latestEvent.status).toBe('ShipmentDispatched');
    expect(latestEvent.currency).toBe('USD');
  });

  it('should simulate an inbound WooCommerce order and reactively update metrics and feed', () => {
    const initialOrders = component.totalSyncedOrders();
    const initialFeedLength = component.ingestionEvents().length;

    component.simulateInboundOrder('WooCommerce');

    expect(component.totalSyncedOrders()).toBe(initialOrders + 1);
    expect(component.totalActiveDeals()).toBe(initialOrders + 1);
    expect(component.totalShipments()).toBe(initialOrders + 1);
    expect(component.ingestionEvents().length).toBe(initialFeedLength + 1);

    const latestEvent = component.ingestionEvents()[0];
    expect(latestEvent.platform).toBe('WooCommerce');
    expect(latestEvent.orderNumber).toMatch(/^WC-\d+$/);
    expect(latestEvent.status).toBe('ShipmentDispatched');
    expect(latestEvent.currency).toBe('EUR');
  });

  it('should format currency according to current locale (Frontier 1 & 4 integration)', () => {
    // English (USD default)
    translation.setLocale('en-US');
    const usdFormatted = translation.formatCurrency(285.50, 'USD');
    expect(usdFormatted).toContain('285.50');

    // French locale formatting
    translation.setLocale('fr-FR');
    const eurFormatted = translation.formatCurrency(418.00, 'EUR');
    expect(eurFormatted).toContain('418');
  });

  it('should trigger catalog sync and update timestamp and product count', async () => {
    vi.useFakeTimers();
    const initialProducts = component.totalCatalogProducts();

    component.triggerCatalogSync('all');
    expect(component.isSyncing()).toBe(true);

    vi.advanceTimersByTime(700);

    expect(component.isSyncing()).toBe(false);
    expect(component.totalCatalogProducts()).toBe(initialProducts + 2); // +1 Shopify, +1 WooCommerce
    expect(component.toastMessage()).toContain('Catalog synchronization completed');
    vi.useRealTimers();
  });
});
