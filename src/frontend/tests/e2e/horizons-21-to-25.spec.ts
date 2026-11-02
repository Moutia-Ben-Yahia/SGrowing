import { test, expect } from '@playwright/test';

test.describe('Horizons 21 to 25 Enterprise Growth OS E2E Suites', () => {

  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h21_25');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_enterprise_h21_25',
        name: 'Enterprise Growth Lead',
        email: 'growth@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner']
      }));
    });
  });

  test('Horizon 21: App Marketplace & Sandboxed Custom Extension Engine', async ({ page }) => {
    await page.goto('/integrations/marketplace');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('App Marketplace & Extensions');
    await expect(page.locator('text=HubSpot Two-Way CRM Connector').first()).toBeVisible();

    // Filter by AI category
    await page.click('button:text("AI")');
    await expect(page.locator('text=Gemini 2.5 Multi-Modal Agent Copilot')).toBeVisible();

    // Switch back to All
    await page.click('button:text("All")');

    // Click Install on Shopify
    const shopifyInstallBtn = page.locator('[data-testid="btn-install-app_shopify_commerce"]');
    if (await shopifyInstallBtn.isVisible()) {
      await shopifyInstallBtn.click();
      await expect(page.locator('[data-testid="install-modal"]')).toBeVisible();

      // Enter API key and confirm
      await page.fill('[data-testid="input-api-key"]', 'shpat_mock_token_123');
      await page.click('[data-testid="btn-confirm-install"]');
      await expect(page.locator('[data-testid="install-modal"]')).not.toBeVisible();
    }

    // Switch to Installed tab
    await page.click('[data-testid="tab-installed"]');
    await expect(page.locator('[data-testid="installed-apps-list"]')).toBeVisible();
  });

  test('Horizon 22: Real-Time WebRTC Voice SDR & Duplex Audio Streaming', async ({ page }) => {
    await page.goto('/voice/webrtc-sdr');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Real-Time WebRTC Voice SDR');
    await expect(page.locator('text=Sarah Sales SDR').first()).toBeVisible();

    // Start Call
    await page.click('[data-testid="btn-start-call"]');
    await expect(page.locator('text=Active').first()).toBeVisible();
    await expect(page.locator('text=AI SDR (Sarah Sales SDR)').first()).toBeVisible();

    // Simulate Barge-In
    await page.click('[data-testid="btn-simulate-bargein"]');
    await expect(page.locator('text=Wait, tell me about pricing').first()).toBeVisible();

    // End call
    await page.click('[data-testid="btn-end-call"]');
    await expect(page.locator('text=Completed').first()).toBeVisible();
  });

  test('Horizon 23: Executive Revenue Intelligence & Cohort Churn Matrix', async ({ page }) => {
    await page.goto('/analytics/executive');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Executive Revenue Intelligence');
    await expect(page.locator('[data-testid="kpi-cards"]')).toBeVisible();
    await expect(page.locator('text=ARR Run Rate')).toBeVisible();
    await expect(page.locator('text=Monthly Recurring Revenue (MRR) Waterfall')).toBeVisible();
    await expect(page.locator('[data-testid="cohort-table-wrapper"]')).toBeVisible();
    await expect(page.locator('text=2026-05').first()).toBeVisible();
  });

  test('Horizon 24: Self-Hosted Enterprise License Activation & Air-Gapped Key Validator', async ({ page }) => {
    await page.goto('/tenancy/licensing');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Self-Hosted Enterprise Licensing');
    await expect(page.locator('[data-testid="license-kpis"]')).toBeVisible();
    await expect(page.locator('text=Enterprise Global Inc.').first()).toBeVisible();
    await expect(page.locator('text=Air-Gapped Validated').first()).toBeVisible();

    // Activate new license key
    await page.fill('[data-testid="input-license-org"]', 'Sovereign Bank International');
    await page.fill('[data-testid="input-license-key"]', 'SG-ENT-SOVEREIGN-BANK-999');
    await page.click('[data-testid="btn-activate-license"]');

    await expect(page.locator('[data-testid="activation-msg"]')).toBeVisible();
    await expect(page.locator('text=Sovereign Bank International').first()).toBeVisible();
  });

  test('Horizon 25: AI Search & Answer Engine Optimization (GEO/AEO) Visibility Tracker', async ({ page }) => {
    await page.goto('/seo/geo-visibility');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('AI Search & GEO/AEO Visibility Tracker');
    await expect(page.locator('[data-testid="geo-kpis"]')).toBeVisible();
    await expect(page.locator('text=Share of Model Voice (SoMV)')).toBeVisible();
    await expect(page.locator('[data-testid="citations-list"]')).toBeVisible();

    // Open audit modal and audit new query
    await page.click('[data-testid="btn-open-audit"]');
    await expect(page.locator('[data-testid="audit-modal"]')).toBeVisible();

    await page.fill('[data-testid="input-audit-query"]', 'Best AI SDR and multi-agent growth tool');
    await page.click('[data-testid="btn-confirm-audit"]');

    await expect(page.locator('text=Best AI SDR and multi-agent growth tool').first()).toBeVisible();
  });

});
