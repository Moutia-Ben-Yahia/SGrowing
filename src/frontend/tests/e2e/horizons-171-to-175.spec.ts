import { test, expect } from '@playwright/test';

test.describe('Horizons 171 to 175 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h171_175');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h171_175',
        name: 'Chief Customer & Infrastructure Architect',
        email: 'evpn-commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H171: SPIFF Gamification Cockpit renders and launches campaign', async ({ page }) => {
    await page.goto('/sales/spiff-gamification');
    await expect(page.locator('[data-testid="spiff-gamification-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Sales Performance SPIFF & Real-Time Deal Gamification Escrow')).toBeVisible();
    await expect(page.locator('[data-testid="campaigns-table"]')).toBeVisible();

    await page.click('button[data-testid="launch-campaign-btn"]');
    await expect(page.locator('text=Q1 AI Agent Enterprise Tier Rush')).toBeVisible();
  });

  test('H172: Vehicle Audio DSP Cockpit renders and places vehicle ad bid', async ({ page }) => {
    await page.goto('/advertising/vehicle-audio-dsp');
    await expect(page.locator('[data-testid="vehicle-audio-dsp-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Connected Vehicle Contextual Audio DSP Ads & In-Cabin Commute Attribution')).toBeVisible();
    await expect(page.locator('[data-testid="bids-table"]')).toBeVisible();

    await page.click('button[data-testid="bid-slot-btn"]');
    await expect(page.locator('text=Porsche Taycan (PCM 6.0)')).toBeVisible();
  });

  test('H173: Dereverberation Cockpit renders and profiles room acoustics', async ({ page }) => {
    await page.goto('/voice/dereverberation');
    await expect(page.locator('[data-testid="dereverberation-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous AI Voice Telephony Dynamic Acoustic Room Profiling & Dereverberation Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="profiles-table"]')).toBeVisible();

    await page.click('button[data-testid="profile-room-btn"]');
    await expect(page.locator('text=SmallTiledRoomOrKitchen')).toBeVisible();
  });

  test('H174: Section 174 Cockpit renders and creates amortization schedule', async ({ page }) => {
    await page.goto('/finance/section174');
    await expect(page.locator('[data-testid="section174-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Sovereign R&D Section 174 Amortization & Capitalized Software Ledger')).toBeVisible();
    await expect(page.locator('[data-testid="schedules-table"]')).toBeVisible();

    await page.click('button[data-testid="create-schedule-btn"]');
    await expect(page.locator('text=SCHED-174-2026-US')).toBeVisible();
  });

  test('H175: EVPN VXLAN Cockpit renders and registers VTEP node', async ({ page }) => {
    await page.goto('/infrastructure/evpn-vxlan');
    await expect(page.locator('[data-testid="evpn-vxlan-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Zero-Trust Multi-Cloud eBGP EVPN VXLAN Overlay Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="vteps-table"]')).toBeVisible();

    await page.click('button[data-testid="register-vtep-btn"]');
    await expect(page.locator('text=coreweave-ord1-gpu-01')).toBeVisible();
  });
});
