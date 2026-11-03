import { test, expect } from '@playwright/test';

test.describe('Horizons 151 to 155 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h151_155');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h151_155',
        name: 'Chief Commercial & Infrastructure Architect',
        email: 'ebpf-commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H151: CAB Matrix Cockpit renders and aligns new executive sponsor', async ({ page }) => {
    await page.goto('/sales/cab-matrix');
    await expect(page.locator('[data-testid="cab-matrix-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous B2B Customer Advisory Board & Sponsor Matrix')).toBeVisible();
    await expect(page.locator('[data-testid="pairings-table"]')).toBeVisible();

    await page.click('button[data-testid="align-sponsor-btn"]');
    await expect(page.locator('text=Carlos Mendez (VP Digital)')).toBeVisible();
  });

  test('H152: In-Car Ad Cockpit renders and submits voice RTB ad bid', async ({ page }) => {
    await page.goto('/advertising/in-car-ads');
    await expect(page.locator('[data-testid="in-car-ad-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous In-Car Infotainment Voice Ad Bidding Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="sessions-table"]')).toBeVisible();

    await page.click('button[data-testid="submit-bid-btn"]');
    await expect(page.locator('text=ADV-BP-PULSE')).toBeVisible();
  });

  test('H153: Voice Intent Fast-Path Cockpit renders and classifies speech utterance', async ({ page }) => {
    await page.goto('/voice/voice-intent');
    await expect(page.locator('[data-testid="voice-intent-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Voice Telephony Acoustic Normalization & Intent Fast-Path')).toBeVisible();
    await expect(page.locator('[data-testid="predictions-table"]')).toBeVisible();

    await page.click('button[data-testid="classify-intent-btn"]');
    await expect(page.locator('text=SHIPPING_REROUTE_CONFIRMATION')).toBeVisible();
  });

  test('H154: Sovereign DST Cockpit renders and computes DST accrual', async ({ page }) => {
    await page.goto('/finance/sovereign-dst');
    await expect(page.locator('[data-testid="sovereign-dst-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Sovereign DST & OECD Pillar 1/2 Minimum Tax Ledger')).toBeVisible();
    await expect(page.locator('[data-testid="dst-records-table"]')).toBeVisible();

    await page.click('button[data-testid="compute-dst-btn"]');
    await expect(page.locator('text=MNE-IT-DIGITAL')).toBeVisible();
  });

  test('H155: Cilium eBPF Cockpit renders and applies new network policy', async ({ page }) => {
    await page.goto('/infrastructure/cilium-ebpf');
    await expect(page.locator('[data-testid="cilium-ebpf-cockpit"]')).toBeVisible();
    await expect(page.locator('text=Autonomous Cilium eBPF Service Mesh & WireGuard Transit Mesh')).toBeVisible();
    await expect(page.locator('[data-testid="policies-table"]')).toBeVisible();

    await page.click('button[data-testid="apply-policy-btn"]');
    await expect(page.locator('text=fastpath-udp-kernel-bypass')).toBeVisible();
  });
});
