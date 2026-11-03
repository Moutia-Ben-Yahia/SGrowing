import { test, expect } from '@playwright/test';

test.describe('Horizons 186 to 190 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h186_190');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h186_190',
        name: 'Chief Commercial & Storage Architect',
        email: 'nvme-commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H186: Quota Capacity Cockpit renders and simulates territory rebalance', async ({ page }) => {
    await page.goto('/sales/quota-capacity');
    await expect(page.locator('[data-testid="quota-capacity-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 186: Sales Quota Capacity & Territory Heatmap Arbiter')).toBeVisible();
    await expect(page.locator('[data-testid="territories-count"]')).toBeVisible();

    await page.click('button[data-testid="rebalance-btn"]');
    await expect(page.locator('[data-testid="rebalance-result-box"]')).toBeVisible();
    await expect(page.locator('text=New Headcount:')).toBeVisible();
  });

  test('H187: Store Footfall Lift Cockpit renders and runs geofence lift estimation', async ({ page }) => {
    await page.goto('/advertising/store-footfall-lift');
    await expect(page.locator('[data-testid="store-footfall-lift-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 187: Multi-Touch Store Footfall & Geofence Polygon Lift Matrix')).toBeVisible();
    await expect(page.locator('[data-testid="stores-count"]')).toBeVisible();

    await page.click('button[data-testid="calculate-lift-btn"]');
    await expect(page.locator('[data-testid="lift-analysis-box"]')).toBeVisible();
    await expect(page.locator('[data-testid="cpiv-value"]')).toBeVisible();
  });

  test('H188: Dialect Synthesis Cockpit renders and synthesizes vernacular speech', async ({ page }) => {
    await page.goto('/voice/dialect-synthesis');
    await expect(page.locator('[data-testid="dialect-synthesis-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 188: Real-Time Voice Dialect Idiom & Vernacular Synthesizer Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="profiles-count"]')).toBeVisible();

    await page.click('button[data-testid="synthesize-utterance-btn"]');
    await expect(page.locator('[data-testid="adapted-speech-box"]')).toBeVisible();
    await expect(page.locator('text=Synthesized Vernacular Speech:')).toBeVisible();
  });

  test('H189: STTR Treaty Tax Cockpit renders and assesses intragroup payment', async ({ page }) => {
    await page.goto('/finance/sttr-treaty-tax');
    await expect(page.locator('[data-testid="sttr-treaty-tax-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 189: Sovereign Pillar Two Subject to Tax Rule (STTR) & Treaty Ledger')).toBeVisible();
    await expect(page.locator('[data-testid="payments-count"]')).toBeVisible();

    await page.click('button[data-testid="assess-payment-btn"]');
    await expect(page.locator('[data-testid="sttr-evaluation-box"]')).toBeVisible();
    await expect(page.locator('[data-testid="top-up-tax-amount"]')).toBeVisible();

    await page.click('button[data-testid="issue-cert-btn"]');
    await expect(page.locator('text=Cryptographic Certificate Issued (SHA-256)')).toBeVisible();
  });

  test('H190: NVMe-oF Storage Cockpit renders and simulates ANA multipath failover', async ({ page }) => {
    await page.goto('/infrastructure/nvme-over-fabrics');
    await expect(page.locator('[data-testid="nvme-over-fabrics-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 190: High-Throughput NVMe-over-Fabrics (NVMe-oF) Storage Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="subsystems-count"]')).toBeVisible();
    await expect(page.locator('[data-testid="throughput-stat"]')).toBeVisible();

    await page.click('button[data-testid="simulate-failover-btn"]');
    await expect(page.locator('[data-testid="failover-event-box"]')).toBeVisible();
    await expect(page.locator('text=Zero Data Loss Verified')).toBeVisible();
  });
});
