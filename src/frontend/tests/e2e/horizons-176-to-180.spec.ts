import { test, expect } from '@playwright/test';

test.describe('Horizons 176 to 180 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h176_180');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h176_180',
        name: 'Chief Customer & Infrastructure Architect',
        email: 'srv6-commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H176: Deal Autopsy Cockpit renders and analyzes lost opportunity', async ({ page }) => {
    await page.goto('/sales/deal-autopsy');
    await expect(page.locator('[data-testid="deal-autopsy-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous B2B Win-Rate Deal Autopsy & Lost Opportunity Signal Forensic Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="autopsies-table"]')).toBeVisible();

    await page.click('button[data-testid="analyze-deal-btn"]');
    await expect(page.locator('text=OmniHealth BioSystems')).toBeVisible();
  });

  test('H177: Sponsored Brand Ads Cockpit renders and creates multi-product campaign', async ({ page }) => {
    await page.goto('/advertising/sponsored-brand-ads');
    await expect(page.locator('[data-testid="sponsored-brand-ads-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Retail Media Sponsored Brand Multi-Product Banner DSP Auction')).toBeVisible();
    await expect(page.locator('[data-testid="campaigns-table"]')).toBeVisible();

    await page.click('button[data-testid="create-campaign-btn"]');
    await expect(page.locator('text=LuxeGlow Skincare Organics')).toBeVisible();
  });

  test('H178: Prosody Empathy Cockpit renders and shifts call tone', async ({ page }) => {
    await page.goto('/voice/prosody-empathy');
    await expect(page.locator('[data-testid="prosody-empathy-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Speech-to-Speech Prosody & Emotional Empathy Tone Shifter Engine')).toBeVisible();
    await expect(page.locator('[data-testid="events-table"]')).toBeVisible();

    await page.click('button[data-testid="shift-tone-btn"]');
    await expect(page.locator('text=CALL-SIP-8891')).toBeVisible();
  });

  test('H179: Intercompany Netting Cockpit renders and adds intercompany invoice', async ({ page }) => {
    await page.goto('/finance/intercompany-netting');
    await expect(page.locator('[data-testid="intercompany-netting-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Real-Time Intercompany Trade Settlement & Multilateral Netting Ledger')).toBeVisible();
    await expect(page.locator('[data-testid="invoices-table"]')).toBeVisible();

    await page.click('button[data-testid="add-invoice-btn"]');
    await expect(page.locator('text=SGrowing Japan KK (Tokyo)')).toBeVisible();
  });

  test('H180: SRv6 Traffic Cockpit renders and creates SRv6 policy path', async ({ page }) => {
    await page.goto('/infrastructure/srv6-traffic');
    await expect(page.locator('[data-testid="srv6-traffic-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Multi-Cloud Segment Routing over IPv6 (SRv6) Traffic Engineering Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="policies-table"]')).toBeVisible();

    await page.click('button[data-testid="create-policy-btn"]');
    await expect(page.locator('text=Asia-Pacific Core Transit Path')).toBeVisible();
  });
});
