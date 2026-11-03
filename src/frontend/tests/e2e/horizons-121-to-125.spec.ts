import { test, expect } from '@playwright/test';

test.describe('Horizons 121 to 125 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h121_125');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h121_125',
        name: 'Chief Commercial & FinOps Architect',
        email: 'commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H121: Direct Mail ABM Cockpit renders and dispatches gift box', async ({ page }) => {
    await page.goto('/sales/direct-mail-mesh');
    await expect(page.locator('[data-testid="direct-mail-mesh-cockpit"]')).toBeVisible();
    await expect(page.locator('text=ABM Direct Mail & Executive Gifting Mesh')).toBeVisible();
    await expect(page.locator('text=Curated Gift Tiers')).toBeVisible();

    await page.click('button:has-text("Dispatch Executive Gift Box")');
    await expect(page.locator('text=Elena Rostova (CTO)')).toBeVisible();
  });

  test('H122: Spatial Ad Bidding Cockpit renders and simulates spatial RTB bid', async ({ page }) => {
    await page.goto('/advertising/spatial-ad-bidding');
    await expect(page.locator('[data-testid="spatial-ad-bidding-cockpit"]')).toBeVisible();
    await expect(page.locator('text=In-Game & Metaverse 3D Spatial Ad Bidding Mesh')).toBeVisible();
    await expect(page.locator('text=Virtual 3D Inventory Zones')).toBeVisible();

    await page.click('button:has-text("Simulate Spatial RTB Bid")');
    await expect(page.locator('text=Hyperion Neural Tech')).toBeVisible();
  });

  test('H123: Speech Streaming Mesh Cockpit displays sessions and triggers barge-in', async ({ page }) => {
    await page.goto('/voice/speech-streaming-mesh');
    await expect(page.locator('[data-testid="speech-streaming-mesh-cockpit"]')).toBeVisible();
    await expect(page.locator('text=AI Voice Telephony Speech-to-Speech (S2S) Audio Streaming Mesh')).toBeVisible();
    await expect(page.locator('text=Active Neural Audio Streams')).toBeVisible();

    await page.click('button:has-text("Trigger Instant Barge-In Cutoff")');
    await expect(page.locator('text=Yes, and we can also support multi-cloud deployment...')).toBeVisible();
  });

  test('H124: Asset Depreciation Cockpit displays asset register and calculates depreciation', async ({ page }) => {
    await page.goto('/finance/asset-depreciation');
    await expect(page.locator('[data-testid="asset-depreciation-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Dynamic CapEx Depreciation & Asset Lifecycle Ledger')).toBeVisible();
    await expect(page.locator('text=Capital Asset Register')).toBeVisible();

    await page.click('button:has-text("Calculate Next Period Depreciation")');
    await expect(page.locator('text=$12,750')).toBeVisible();
  });

  test('H125: Secret Rotation Mesh Cockpit displays secrets and issues ephemeral cert', async ({ page }) => {
    await page.goto('/infrastructure/secret-rotation-mesh');
    await expect(page.locator('[data-testid="secret-rotation-mesh-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Multi-Cloud Secret Rotation & Ephemeral mTLS Mesh')).toBeVisible();
    await expect(page.locator('text=Enterprise Managed Secrets')).toBeVisible();

    await page.click('button:has-text("Issue 15-Min mTLS Cert")');
    await expect(page.locator('text=EDGE-POP-IAD-02')).toBeVisible();
  });
});
