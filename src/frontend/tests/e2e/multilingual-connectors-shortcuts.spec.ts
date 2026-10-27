import { test, expect } from '@playwright/test';

test.describe('Frontiers 1-4 E2E: Multilingual LTR, Shortcuts & Commerce Connectors', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_1',
        email: 'admin@sgrowing.io',
        name: 'Admin E2E'
      }));
    });
  });

  test('Frontier 1: should enforce strict LTR direction and render language switcher', async ({ page }) => {
    await page.goto('/');

    // 1. Verify strict LTR attribute on html
    const htmlDir = await page.locator('html').getAttribute('dir');
    expect(htmlDir).toBe('ltr');

    // 2. Language switcher component should exist
    const switcher = page.locator('[data-testid="language-switcher"]');
    await expect(switcher).toBeVisible();

    // 3. Switch to Français
    const frBtn = page.locator('[data-testid="locale-btn-fr-FR"]');
    await expect(frBtn).toBeVisible();
    await frBtn.click();

    // Verify document direction remains strictly LTR and lang is fr-FR
    const updatedDir = await page.locator('html').getAttribute('dir');
    expect(updatedDir).toBe('ltr');
    const updatedLang = await page.locator('html').getAttribute('lang');
    expect(updatedLang).toBe('fr-FR');
  });

  test('Frontier 3: should open Quick Macro Search modal on keyboard shortcut', async ({ page }) => {
    await page.goto('/');

    // Trigger Ctrl+Shift+Space
    await page.keyboard.press('Control+Shift+Space');

    // Verify Quick Macro modal opens
    const modal = page.locator('text=Quick Macro Search');
    await expect(modal).toBeVisible();

    // Press Escape to dismiss
    await page.keyboard.press('Escape');
    await expect(modal).not.toBeVisible();
  });

  test('Frontier 4: should navigate to E-Commerce Connectors Cockpit and interact with sync', async ({ page }) => {
    await page.goto('/commerce/connectors');

    // 1. Verify Header and Subtitle
    await expect(page.locator('header')).toContainText('E-Commerce Storefront Connectors Cockpit');

    // 2. Verify Shopify & WooCommerce Cards
    await expect(page.locator('text=Shopify Connector')).toBeVisible();
    await expect(page.locator('text=WooCommerce Connector')).toBeVisible();

    // 3. Verify KPI cards
    await expect(page.locator('text=Total Synced Orders')).toBeVisible();
    await expect(page.locator('text=Active CRM Deals')).toBeVisible();
    await expect(page.locator('text=COD Shipments Dispatched')).toBeVisible();

    // 4. Test Webhook Simulation
    const simulateBtn = page.locator('button:has-text("Simulate Webhook")').first();
    await simulateBtn.click();

    // Verify Toast notification
    await expect(page.locator('text=successfully mapped to CRM Deal')).toBeVisible();

    // 5. Verify Language Switcher is present in Cockpit header
    const cockpitLangSwitcher = page.locator('[data-testid="language-switcher"]');
    await expect(cockpitLangSwitcher).toBeVisible();
  });
});
