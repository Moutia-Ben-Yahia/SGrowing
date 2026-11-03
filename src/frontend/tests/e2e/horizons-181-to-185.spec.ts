import { test, expect } from '@playwright/test';

test.describe('Horizons 181 to 185 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h181_185');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h181_185',
        name: 'Chief Cognitive & Infrastructure Architect',
        email: 'roce-commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H181: Rep Ramp Cockpit renders and generates shadow guidance', async ({ page }) => {
    await page.goto('/sales/rep-ramp');
    await expect(page.locator('[data-testid="rep-ramp-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 181: Sales Rep Ramp Acceleration & Shadow Co-Pilot')).toBeVisible();
    await expect(page.locator('[data-testid="active-reps-count"]')).toBeVisible();

    await page.click('button[data-testid="generate-guidance-btn"]');
    await expect(page.locator('[data-testid="whisper-guidance-box"]')).toBeVisible();
    await expect(page.locator('text=Budget & Pricing Constraint')).toBeVisible();
  });

  test('H182: Cross-Device Sync Cockpit renders and matches offline POS conversion', async ({ page }) => {
    await page.goto('/advertising/cross-device-sync');
    await expect(page.locator('[data-testid="cross-device-sync-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 182: Cross-Device Graph Sync & CAPI Offline Conversion Matcher')).toBeVisible();
    await expect(page.locator('[data-testid="active-clusters-count"]')).toBeVisible();

    await page.click('button[data-testid="match-offline-btn"]');
    await expect(page.locator('[data-testid="matched-result-box"]')).toBeVisible();
    await expect(page.locator('text=Forwarded to Meta CAPI & Google OCI')).toBeVisible();
  });

  test('H183: Vocal Tremor Cockpit renders and analyzes acoustic stress', async ({ page }) => {
    await page.goto('/voice/vocal-tremor');
    await expect(page.locator('[data-testid="vocal-tremor-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 183: Whisper Voice Isolation & Micro-Vocal Tremor Stress Detector')).toBeVisible();
    await expect(page.locator('[data-testid="audited-sessions-count"]')).toBeVisible();

    await page.click('button[data-testid="analyze-acoustics-btn"]');
    await expect(page.locator('[data-testid="stress-result-meter"]')).toBeVisible();
    await expect(page.locator('[data-testid="supervisor-alert-banner"]')).toBeVisible();
  });

  test('H184: Globe Top-Up Tax Cockpit renders and calculates top-up liability', async ({ page }) => {
    await page.goto('/finance/globe-top-up-tax');
    await expect(page.locator('[data-testid="globe-top-up-tax-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 184: OECD Pillar Two GloBE Top-Up Tax & Safe Harbor Ledger')).toBeVisible();
    await expect(page.locator('[data-testid="jurisdictions-count"]')).toBeVisible();

    await page.click('button[data-testid="calculate-top-up-btn"]');
    await expect(page.locator('[data-testid="top-up-result-box"]')).toBeVisible();
    await expect(page.locator('[data-testid="sbie-carveout-val"]')).toBeVisible();
  });

  test('H185: RoCEv2 Fabric Cockpit renders and simulates deadlock watchdog drain', async ({ page }) => {
    await page.goto('/infrastructure/rocev2-fabric');
    await expect(page.locator('[data-testid="rocev2-fabric-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Horizon 185: Multi-Cloud RoCEv2 / RDMA over Converged Ethernet Fabric Arbiter')).toBeVisible();
    await expect(page.locator('[data-testid="roce-nodes-count"]')).toBeVisible();

    await page.click('button[data-testid="ingest-telemetry-btn"]');
    await expect(page.locator('[data-testid="deadlock-alert-box"]')).toBeVisible();

    await page.click('button[data-testid="trigger-watchdog-btn"]');
    await expect(page.locator('text=Queue Drained Successfully: Credit loop restored.')).toBeVisible();
  });
});
