import { test, expect } from '@playwright/test';

test.describe('Horizons 141 to 145 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h141_145');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h141_145',
        name: 'Chief Commercial & Network Architect',
        email: 'bgp-commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H141: Buying Committee Cockpit renders and schedules intervention', async ({ page }) => {
    await page.goto('/sales/buying-committee');
    await expect(page.locator('[data-testid="buying-committee-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Buying Committee Influence Graph & Sentiment Journey')).toBeVisible();
    await expect(page.locator('[data-testid="stakeholder-table"]')).toBeVisible();

    await page.click('button[data-testid="intervention-btn"]');
    await expect(page.locator('text=Custom ROI business case review for Dr. Marcus Vance')).toBeVisible();
  });

  test('H142: Audio DAI Cockpit renders and inserts dynamic slot', async ({ page }) => {
    await page.goto('/advertising/audio-dai');
    await expect(page.locator('[data-testid="audio-dai-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Dynamic Audio Ad Insertion (DAI) & Attribution Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="audio-slots-table"]')).toBeVisible();

    await page.click('button[data-testid="insert-slot-btn"]');
    await expect(page.locator('text=FinTech Leaders Podcast')).toBeVisible();
  });

  test('H143: Telephony Whisper Cockpit renders and triggers takeover', async ({ page }) => {
    await page.goto('/voice/telephony-whisper');
    await expect(page.locator('[data-testid="telephony-whisper-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Dual-Channel Telephony Whisper Coach & Supervisory Takeover')).toBeVisible();
    await expect(page.locator('[data-testid="sessions-table"]')).toBeVisible();

    await page.click('button[data-testid="takeover-btn"]');
    await expect(page.locator('text=FULL_TAKEOVER')).toBeVisible();
  });

  test('H144: Tax Exemption Cockpit renders and validates certificate', async ({ page }) => {
    await page.goto('/finance/tax-exemption');
    await expect(page.locator('[data-testid="tax-exemption-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Global Sovereign Tax Exemption & Reverse Charge VAT Clearance')).toBeVisible();
    await expect(page.locator('[data-testid="certificates-table"]')).toBeVisible();

    await page.click('button[data-testid="validate-cert-btn"]');
    await expect(page.locator('text=Airbus Defence and Space SAS')).toBeVisible();
  });

  test('H145: BGP BFD Cockpit renders and simulates fast failover', async ({ page }) => {
    await page.goto('/infrastructure/bgp-bfd');
    await expect(page.locator('[data-testid="bgp-bfd-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Multi-Cloud BGP Anycast BFD Fast-Failover Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="bfd-sessions-table"]')).toBeVisible();

    await page.click('button[data-testid="simulate-failover-btn"]');
    await expect(page.locator('text=POISONED')).toBeVisible();
  });
});
