import { test, expect } from '@playwright/test';

test.describe('Horizons 156 to 160 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h156_160');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h156_160',
        name: 'Chief Customer & Infrastructure Architect',
        email: 'wasm-commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H156: Value Realization Cockpit renders and generates QBR deck', async ({ page }) => {
    await page.goto('/sales/value-realization');
    await expect(page.locator('[data-testid="value-realization-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous B2B Value Realization & Customer Success QBR Deck Generator')).toBeVisible();
    await expect(page.locator('[data-testid="decks-table"]')).toBeVisible();

    await page.click('button[data-testid="generate-qbr-btn"]');
    await expect(page.locator('text=Starlight Financial Network')).toBeVisible();
  });

  test('H157: Weather Ad Triggers Cockpit renders and arms weather trigger', async ({ page }) => {
    await page.goto('/advertising/weather-triggers');
    await expect(page.locator('[data-testid="weather-triggers-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Dynamic Product Ads (DPA) Weather Context Engine')).toBeVisible();
    await expect(page.locator('[data-testid="rules-table"]')).toBeVisible();

    await page.click('button[data-testid="trigger-campaign-btn"]');
    await expect(page.locator('text=CAMP-BEVERAGE-HOTTEA')).toBeVisible();
  });

  test('H158: Telephony Disfluency Cockpit renders and analyzes call cadence', async ({ page }) => {
    await page.goto('/voice/telephony-disfluency');
    await expect(page.locator('[data-testid="telephony-disfluency-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Telephony Silence & Disfluency Diagnostic Neural Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="events-table"]')).toBeVisible();

    await page.click('button[data-testid="analyze-cadence-btn"]');
    await expect(page.locator('text=CALL-DISF-903')).toBeVisible();
  });

  test('H159: Export Compliance Cockpit renders and screens export transaction', async ({ page }) => {
    await page.goto('/finance/export-compliance');
    await expect(page.locator('[data-testid="export-compliance-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Sovereign Export Control & Dual-Use EAR/ITAR Compliance Ledger')).toBeVisible();
    await expect(page.locator('[data-testid="screenings-table"]')).toBeVisible();

    await page.click('button[data-testid="screen-transaction-btn"]');
    await expect(page.locator('text=NTT Communications Corp')).toBeVisible();
  });

  test('H160: Wasm Sandbox Cockpit renders and deploys tenant micro-VM', async ({ page }) => {
    await page.goto('/infrastructure/wasm-sandbox');
    await expect(page.locator('[data-testid="wasm-sandbox-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Multi-Cloud WebAssembly Sandbox & Micro-VM Arbiter')).toBeVisible();
    await expect(page.locator('[data-testid="workers-table"]')).toBeVisible();

    await page.click('button[data-testid="deploy-worker-btn"]');
    await expect(page.locator('text=speech-acoustic-filter.wasm')).toBeVisible();
  });
});
