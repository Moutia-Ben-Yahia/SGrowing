import { test, expect } from '@playwright/test';

test.describe('Horizons 106 to 110 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h106_110');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h106_110',
        name: 'Chief Commercial & FinOps Architect',
        email: 'commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H106: Sales Territory Rebalancing Cockpit renders and simulates equilibrium', async ({ page }) => {
    await page.goto('/sales/territory-rebalancing');
    await expect(page.locator('[data-testid="territory-rebalancing-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-total-accounts"]')).toContainText('156');
    await expect(page.locator('[data-testid="metric-gini-index"]')).toContainText('0.38');

    await page.click('[data-testid="simulate-rebalance-btn"]');
    await expect(page.locator('[data-testid="metric-gini-index"]')).toContainText('0.14');
    await expect(page.locator('[data-testid="metric-plan-status"]')).toContainText('Simulation Active');

    await page.click('[data-testid="apply-rebalance-btn"]');
    await expect(page.locator('[data-testid="metric-plan-status"]')).toContainText('Scheduled (14-Day Grace)');
  });

  test('H107: Connected TV Ad Insertion Cockpit renders and processes bids', async ({ page }) => {
    await page.goto('/advertising/ctv-ad-insertion');
    await expect(page.locator('[data-testid="ctv-ad-insertion-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-active-pods"]')).toContainText('4');
    await expect(page.locator('[data-testid="metric-clearing-cpm"]')).toContainText('$24.00');

    await page.click('[data-testid="simulate-bid-btn"]');
    await expect(page.locator('[data-testid="metric-clearing-cpm"]')).toContainText('$27.50');

    await page.click('[data-testid="simulate-qr-scan-btn"]');
    await expect(page.locator('tbody tr')).toHaveCount(8); // 4 in pods table + 4 in attributions table
  });

  test('H108: AI Agent Screen Diagnostic Cockpit renders and guides cursor', async ({ page }) => {
    await page.goto('/support/screen-diagnostic');
    await expect(page.locator('[data-testid="screen-diagnostic-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-active-sessions"]')).toContainText('3');
    await expect(page.locator('[data-testid="metric-accuracy"]')).toContainText('97.4%');

    await page.click('[data-testid="analyze-frame-btn"]');
    await expect(page.locator('[data-testid="metric-accuracy"]')).toContainText('98.6%');

    await page.click('[data-testid="generate-trajectory-btn"]');
    await expect(page.locator('text=Resolved')).toBeVisible();
  });

  test('H109: Transfer Pricing & Intercompany Ledger Cockpit settles invoices', async ({ page }) => {
    await page.goto('/finance/transfer-pricing');
    await expect(page.locator('[data-testid="transfer-pricing-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-total-entities"]')).toContainText('4');
    await expect(page.locator('[data-testid="metric-compliance"]')).toContainText('100%');

    await page.click('[data-testid="execute-netting-btn"]');
    await expect(page.locator('[data-testid="metric-net-settlement"]')).toContainText('$0.00');
  });

  test('H110: BGP Anycast Edge & Geo-Steering Cockpit overrides routing', async ({ page }) => {
    await page.goto('/infrastructure/bgp-geo-steering');
    await expect(page.locator('[data-testid="bgp-geo-steering-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-total-pops"]')).toContainText('4');
    await expect(page.locator('[data-testid="metric-healthy-pops"]')).toContainText('3 / 4');

    await page.click('[data-testid="probe-pops-btn"]');
    await expect(page.locator('[data-testid="metric-avg-latency"]')).toContainText('29 ms');

    await page.click('[data-testid="override-steering-btn"]');
    await expect(page.locator('[data-testid="metric-overrides"]')).toContainText('1');
    await expect(page.locator('text=Overridden')).toBeVisible();
  });

});
