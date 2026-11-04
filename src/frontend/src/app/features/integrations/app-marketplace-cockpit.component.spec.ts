// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { AppMarketplaceCockpitComponent } from './app-marketplace-cockpit.component';

describe('AppMarketplaceCockpitComponent', () => {
  let component: AppMarketplaceCockpitComponent;

  beforeEach(() => {
    component = new AppMarketplaceCockpitComponent();
  });

  it('should create and initialize catalog', () => {
    expect(component).toBeTruthy();
    expect(component.catalog().length).toBeGreaterThanOrEqual(5);
  });

  it('should filter apps by category', () => {
    component.selectedCategory.set('CRM');
    expect(component.filteredCatalog().every(a => a.category === 'CRM')).toBe(true);

    component.selectedCategory.set('AI');
    expect(component.filteredCatalog().every(a => a.category === 'AI')).toBe(true);
  });

  it('should toggle install and uninstall', () => {
    const shopifyApp = component.catalog().find(a => a.appId === 'app_shopify_commerce')!;
    expect(component.isInstalled('app_shopify_commerce')).toBe(false);

    component.openInstallModal(shopifyApp);
    component.tempApiKey = 'test_token';
    component.confirmInstall();

    expect(component.isInstalled('app_shopify_commerce')).toBe(true);

    component.uninstallApp('app_shopify_commerce');
    expect(component.isInstalled('app_shopify_commerce')).toBe(false);
  });

  it('should return app name or fallback to appId via getAppName', () => {
    expect(component.getAppName('app_hubspot_sync')).toBe('HubSpot Two-Way CRM Connector');
    expect(component.getAppName('unknown_app_id')).toBe('unknown_app_id');
  });
});
