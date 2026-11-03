import { test, expect } from '@playwright/test';

test.describe('Horizons 86 to 90 Enterprise Growth OS E2E Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h86_90');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h86_90',
        name: 'Chief Commercial & FinOps Architect',
        email: 'commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('Horizon 86: Autonomous Account Expansion & Upsell Opportunity Radar', async ({ page }) => {
    await page.goto('/intelligence/account-expansion');

    const rootDir = await page.locator('[data-testid="account-expansion-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Account Expansion & Upsell Opportunity Radar');
    await expect(page.locator('[data-testid="kpi-predicted-nrr"]')).toContainText('136.4%');
    await expect(page.locator('[data-testid="kpi-ready-accounts"]')).toContainText('3 / 3');

    // Generate proposal for first account
    await page.locator('[data-testid="btn-generate-proposal"]').first().click();
    await expect(page.locator('[data-testid="proposal-success-badge"]').first()).toBeVisible();
    await expect(page.locator('[data-testid="kpi-proposals-dispatched"]')).toContainText('1');
  });

  test('Horizon 87: Autonomous Influencer Campaign Affiliate Payout & Escrow', async ({ page }) => {
    await page.goto('/commerce/influencer-escrow-v2');

    const rootDir = await page.locator('[data-testid="influencer-escrow-v2-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Influencer Affiliate Payout & Escrow');
    await expect(page.locator('[data-testid="kpi-escrow-balance"]')).toContainText('$35,500');

    // Release escrow for first campaign
    await page.locator('[data-testid="btn-release-escrow"]').first().click();
    await expect(page.locator('[data-testid="escrow-released-status"]').first()).toBeVisible();
    await expect(page.locator('[data-testid="kpi-escrow-balance"]')).toContainText('$20,500');
    await expect(page.locator('[data-testid="kpi-released-payouts"]')).toContainText('1 / 3');
  });

  test('Horizon 88: Autonomous Voice IVR & Warm Call Transfer Mesh', async ({ page }) => {
    await page.goto('/voice/call-transfer');

    const rootDir = await page.locator('[data-testid="call-transfer-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Voice IVR & Warm Call Transfer Mesh');
    await expect(page.locator('[data-testid="kpi-deflection-rate"]')).toContainText('100.0%');

    // Initiate warm transfer
    await page.locator('[data-testid="btn-initiate-transfer"]').first().click();
    await expect(page.locator('[data-testid="kpi-bridged-transfers"]')).toContainText('1');

    // Complete handoff
    await page.locator('button:has-text("Complete Handoff")').first().click();
    await expect(page.locator('[data-testid="call-transferred-indicator"]').first()).toBeVisible();
  });

  test('Horizon 89: Autonomous Revenue Recognition & ASC 606 Deferred Ledger', async ({ page }) => {
    await page.goto('/finance/asc606-revenue');

    const rootDir = await page.locator('[data-testid="asc606-revenue-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Revenue Recognition & Deferred Ledger');
    await expect(page.locator('[data-testid="kpi-deferred-revenue"]')).toContainText('$570,000');
    await expect(page.locator('[data-testid="kpi-recognized-revenue"]')).toContainText('$150,000');

    // Recognize monthly tranche
    await page.locator('[data-testid="btn-recognize-tranche"]').first().click();
    await expect(page.locator('[data-testid="kpi-deferred-revenue"]')).toContainText('$550,000');
    await expect(page.locator('[data-testid="kpi-recognized-revenue"]')).toContainText('$170,000');
  });

  test('Horizon 90: Multi-Region Secret Rotation & Vault Zeroization Mesh', async ({ page }) => {
    await page.goto('/security/secret-vault-mesh');

    const rootDir = await page.locator('[data-testid="secret-vault-mesh-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Multi-Region Secret Rotation & Vault Zeroization Mesh');
    await expect(page.locator('[data-testid="kpi-active-keys"]')).toContainText('4 / 4');

    // Rotate secret
    await page.locator('[data-testid="btn-rotate-secret"]').first().click();
    await expect(page.locator('text=v4').first()).toBeVisible();

    // Zeroize secret
    await page.locator('[data-testid="btn-zeroize-secret"]').first().click();
    await expect(page.locator('[data-testid="zeroized-alert-badge"]').first()).toBeVisible();
    await expect(page.locator('[data-testid="kpi-zeroized-keys"]')).toContainText('1');
    await expect(page.locator('[data-testid="kpi-active-keys"]')).toContainText('3 / 4');
  });
});
