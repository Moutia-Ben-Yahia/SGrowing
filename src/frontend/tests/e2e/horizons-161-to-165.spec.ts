import { test, expect } from '@playwright/test';

test.describe('Horizons 161 to 165 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h161_165');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h161_165',
        name: 'Chief Customer & Infrastructure Architect',
        email: 'mesh-commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H161: Customer Advocacy Cockpit renders and matches reference champion', async ({ page }) => {
    await page.goto('/sales/customer-advocacy');
    await expect(page.locator('[data-testid="customer-advocacy-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous B2B Customer Advocacy & Reference Matchmaking Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="matches-table"]')).toBeVisible();

    await page.click('button[data-testid="match-reference-btn"]');
    await expect(page.locator('text=Starlight Media Network')).toBeVisible();
  });

  test('H162: Micro-Geofence Cockpit renders and defines geofence polygon', async ({ page }) => {
    await page.goto('/advertising/micro-geofence');
    await expect(page.locator('[data-testid="micro-geofence-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Real-Time Micro-Geofencing & Physical Footfall DSP Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="polygons-table"]')).toBeVisible();

    await page.click('button[data-testid="define-polygon-btn"]');
    await expect(page.locator('text=Javits Center NYC')).toBeVisible();
  });

  test('H163: Echo Cancellation Cockpit renders and processes audio DSP frame', async ({ page }) => {
    await page.goto('/voice/echo-cancellation');
    await expect(page.locator('[data-testid="echo-cancellation-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Neural Acoustic Echo Cancellation & Double-Talk Arbiter')).toBeVisible();
    await expect(page.locator('[data-testid="frames-table"]')).toBeVisible();

    await page.click('button[data-testid="process-frame-btn"]');
    await expect(page.locator('text=CALL-RTC-7703')).toBeVisible();
  });

  test('H164: Capital Allocation Cockpit renders and allocates treasury capital', async ({ page }) => {
    await page.goto('/finance/capital-allocation');
    await expect(page.locator('[data-testid="capital-allocation-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Sovereign Capital Allocation & Working Capital Optimizer')).toBeVisible();
    await expect(page.locator('[data-testid="allocations-table"]')).toBeVisible();

    await page.click('button[data-testid="allocate-capital-btn"]');
    await expect(page.locator('text=US Federal Reserve Direct')).toBeVisible();
  });

  test('H165: WireGuard Discovery Cockpit renders and discovers node peer', async ({ page }) => {
    await page.goto('/infrastructure/wireguard-discovery');
    await expect(page.locator('[data-testid="wireguard-discovery-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Zero-Trust WireGuard Peer Discovery & IPsec Fallback Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="peers-table"]')).toBeVisible();

    await page.click('button[data-testid="discover-peer-btn"]');
    await expect(page.locator('text=gcp-europe-west-01')).toBeVisible();
  });
});
