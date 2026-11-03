import { test, expect } from '@playwright/test';

test.describe('Horizons 71 to 75 Enterprise Growth OS E2E Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h71_75');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h71_75',
        name: 'Chief Commercial & FinOps Architect',
        email: 'commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('Horizon 71: Autonomous Multi-Agent Content Repurposing Engine', async ({ page }) => {
    await page.goto('/content/repurposing');

    const rootDir = await page.locator('[data-testid="content-repurposing-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Multi-Agent Content Repurposing Engine');
    await expect(page.locator('[data-testid="stat-derivatives-count"]')).toContainText('3 Formats');
    await expect(page.locator('[data-testid="stat-slide-count"]')).toContainText('5 Slides');

    // Synthesize TikTok / Shorts Script
    await page.click('[data-testid="generate-short-btn"]');
    await expect(page.locator('[data-testid="stat-derivatives-count"]')).toContainText('4 Formats');
    await expect(page.locator('text=TikTok / Shorts').first()).toBeVisible();
  });

  test('Horizon 72: Autonomous Shapley Value Multi-Touch Pipeline Attribution', async ({ page }) => {
    await page.goto('/analytics/attribution-shapley');

    const rootDir = await page.locator('[data-testid="attribution-shapley-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Shapley Value Multi-Touch Pipeline Attribution');
    await expect(page.locator('[data-testid="stat-deal-value"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-top-driver"]')).toContainText('Executive Demo');

    // Rebalance Marketing Budget to Shapley Optimum
    await page.click('[data-testid="rebalance-budget-btn"]');
    await expect(page.locator('[data-testid="stat-budget-shift"]')).toContainText('Optimized');
    await expect(page.locator('text=Scaled (+25% Spend)').first()).toBeVisible();
  });

  test('Horizon 73: Conversational Agent Assist & In-Flight Whisper Coaching', async ({ page }) => {
    await page.goto('/voice/agent-assist');

    const rootDir = await page.locator('[data-testid="agent-assist-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Conversational Agent Assist & In-Flight Whisper Coaching');
    await expect(page.locator('[data-testid="stat-talk-ratio"]')).toBeVisible();
    await expect(page.locator('[data-testid="stat-battlecards-count"]')).toContainText('1 Cards');

    // Trigger Salesforce competitor battlecard
    await page.click('[data-testid="trigger-salesforce-btn"]');
    await expect(page.locator('[data-testid="stat-battlecards-count"]')).toContainText('2 Cards');
    await expect(page.locator('[data-testid="battlecard-salesforce"]')).toBeVisible();
  });

  test('Horizon 74: Autonomous Global Sales Tax & Harmonized Tariff (HS) Calculator', async ({ page }) => {
    await page.goto('/commerce/tariffs');

    const rootDir = await page.locator('[data-testid="tariff-calculator-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Autonomous Global Sales Tax & Harmonized Tariff (HS) Calculator');
    await expect(page.locator('[data-testid="stat-hs-code"]')).toContainText('8471.50');
    await expect(page.locator('[data-testid="stat-vat-rate"]')).toContainText('19% (DE)');

    // Ship to United Kingdom
    await page.click('[data-testid="calculate-uk-btn"]');
    await expect(page.locator('[data-testid="stat-vat-rate"]')).toContainText('20% (GB)');
    await expect(page.locator('[data-testid="stat-total-landed"]')).toContainText('$2,556.00');
  });

  test('Horizon 75: Zero-Trust Ephemeral Credential Vault & JIT Privilege Mesh', async ({ page }) => {
    await page.goto('/security/ephemeral-vault');

    const rootDir = await page.locator('[data-testid="ephemeral-vault-cockpit"]').getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Zero-Trust Ephemeral Credential Vault & JIT Privilege Mesh');
    await expect(page.locator('[data-testid="stat-active-count"]')).toContainText('2 Active');
    await expect(page.locator('[data-testid="stat-revoked-count"]')).toContainText('0');

    // Revoke first token
    await page.locator('[data-testid="revoke-token-btn"]').first().click();
    await expect(page.locator('[data-testid="stat-active-count"]')).toContainText('1 Active');
    await expect(page.locator('[data-testid="stat-revoked-count"]')).toContainText('1');

    // Emergency kill all
    await page.click('[data-testid="emergency-kill-btn"]');
    await expect(page.locator('[data-testid="stat-active-count"]')).toContainText('0 Active');
    await expect(page.locator('[data-testid="stat-revoked-count"]')).toContainText('2');
  });
});
