// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ConnectorMarketplaceStudioComponent } from './connector-marketplace-studio.component';

describe('ConnectorMarketplaceStudioComponent', () => {
  let component: ConnectorMarketplaceStudioComponent;

  beforeEach(() => {
    component = new ConnectorMarketplaceStudioComponent();
  });

  it('should initialize with prepopulated connectors including Shopify, Salla, Zid, YouCan, and WooCommerce', () => {
    expect(component).toBeTruthy();
    expect(component.selectedTier()).toBe('All');
    expect(component.connectors().length).toBe(5);

    const names = component.connectors().map(c => c.name);
    expect(names).toContain('Shopify');
    expect(names).toContain('Salla');
    expect(names).toContain('Zid');
    expect(names).toContain('YouCan');
    expect(names).toContain('WooCommerce');

    expect(component.installedCount()).toBeGreaterThan(0);
    expect(component.transformedOrder()).not.toBeNull();
  });

  it('should filter connectors by tier (RegionalMENA, GlobalEnterprise, OpenSource, All)', () => {
    // Filter RegionalMENA
    component.setTier('RegionalMENA');
    expect(component.selectedTier()).toBe('RegionalMENA');
    const mena = component.filteredConnectors();
    expect(mena.length).toBe(3); // Salla, Zid, YouCan
    expect(mena.every(c => c.tier === 'RegionalMENA')).toBe(true);

    // Filter GlobalEnterprise
    component.setTier('GlobalEnterprise');
    const enterprise = component.filteredConnectors();
    expect(enterprise.length).toBe(1);
    expect(enterprise[0].name).toBe('Shopify');

    // Filter OpenSource
    component.setTier('OpenSource');
    const openSource = component.filteredConnectors();
    expect(openSource.length).toBe(1);
    expect(openSource[0].name).toBe('WooCommerce');

    // Reset All
    component.setTier('All');
    expect(component.filteredConnectors().length).toBe(5);
  });

  it('should open configure modal, save connector configuration and transition status to Installed', () => {
    const salla = component.connectors().find(c => c.id === 'salla');
    expect(salla?.status).toBe('Available');

    component.openConfigureModal(salla!);
    expect(component.showInstallModal()).toBe(true);
    expect(component.selectedConnector()?.id).toBe('salla');

    component.modalStoreDomain = 'perfumes-riyadh.salla.sa';
    component.modalWebhookSecret = 'whsec_prod_secret_123';
    component.saveConnectorConfiguration();

    expect(component.showInstallModal()).toBe(false);
    const updatedSalla = component.connectors().find(c => c.id === 'salla');
    expect(updatedSalla?.status).toBe('Installed');
    expect(updatedSalla?.activeEndpoints).toBeGreaterThanOrEqual(1);
  });

  it('should transform raw Salla webhook JSON to CanonicalOrderPayload with COD detection', () => {
    component.loadPresetSample('salla');
    component.transformOrderPayload();

    const order = component.transformedOrder();
    expect(order).not.toBeNull();
    expect(order?.sourcePlatform).toBe('Salla');
    expect(order?.orderNumber).toBe('SALLA-ORD-8812');
    expect(order?.currency).toBe('SAR');
    expect(order?.totalAmount).toBe(450.0);
    expect(order?.isCashOnDelivery).toBe(true);
    expect(order?.paymentMethod).toBe('CashOnDelivery');
    expect(order?.customer.name).toBe('Faisal Al-Otaibi');
    expect(order?.customer.shippingAddress.city).toBe('Riyadh');
    expect(order?.items.length).toBe(1);
    expect(order?.items[0].sku).toBe('SKU-OUD-ROYAL-50ML');
  });

  it('should transform raw Shopify webhook JSON to CanonicalOrderPayload with prepaid detection', () => {
    component.loadPresetSample('shopify');
    component.transformOrderPayload();

    const order = component.transformedOrder();
    expect(order).not.toBeNull();
    expect(order?.sourcePlatform).toBe('Shopify');
    expect(order?.orderNumber).toBe('#1045');
    expect(order?.currency).toBe('USD');
    expect(order?.totalAmount).toBe(185.0);
    expect(order?.isCashOnDelivery).toBe(false);
    expect(order?.items.length).toBe(2);
    expect(order?.items[0].name).toBe('Artisan Espresso Beans 1kg');
  });

  it('should handle malformed JSON gracefully by setting transformedOrder to null', () => {
    component.testJsonPayload.set('invalid json payload { missing bracket');
    component.transformOrderPayload();

    expect(component.transformedOrder()).toBeNull();
  });
});
