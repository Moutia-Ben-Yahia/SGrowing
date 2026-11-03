import { test, expect } from '@playwright/test';

test.describe('Horizons 111 to 115 Enterprise Growth OS Suites', () => {
  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h111_115');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_growth_commander_h111_115',
        name: 'Chief Commercial & FinOps Architect',
        email: 'commander@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner', 'GrowthArchitect']
      }));
    });
  });

  test('H111: Buyer Intent Rollup Cockpit renders and locks outreach', async ({ page }) => {
    await page.goto('/sales/intent-rollup');
    await expect(page.locator('[data-testid="intent-rollup-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-total-accounts"]')).toContainText('3');
    await expect(page.locator('[data-testid="metric-high-intent"]')).toContainText('2');

    await page.click('[data-testid="simulate-surge-btn"]');
    await expect(page.locator('[data-testid="metric-high-intent"]')).toContainText('3');

    await page.click('[data-testid="lock-outreach-btn"]');
    await expect(page.locator('text=Locked (Protected)')).toHaveCount(2);
  });

  test('H112: Cookieless UID2 Tokenizer Cockpit generates advertising tokens', async ({ page }) => {
    await page.goto('/advertising/uid2-tokenizer');
    await expect(page.locator('[data-testid="uid2-tokenizer-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-total-identities"]')).toContainText('3');
    await expect(page.locator('[data-testid="metric-active-tokens"]')).toContainText('3');

    await page.click('[data-testid="generate-token-btn"]');
    await expect(page.locator('[data-testid="metric-active-tokens"]')).toContainText('4');

    await page.click('[data-testid="stitch-session-btn"]');
    await expect(page.locator('[data-testid="metric-daily-touchpoints"]')).toContainText('8,950');
  });

  test('H113: Conversational AI Agent Whisper Cockpit analyzes speech and approves concessions', async ({ page }) => {
    await page.goto('/voice/agent-whisper');
    await expect(page.locator('[data-testid="agent-whisper-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-active-calls"]')).toContainText('3');
    await expect(page.locator('[data-testid="metric-adherence"]')).toContainText('94.2%');

    await page.click('[data-testid="simulate-speech-btn"]');
    await expect(page.locator('[data-testid="metric-total-whispers"]')).toContainText('1,841');

    await page.click('[data-testid="apply-concession-btn"]');
    await expect(page.locator('text=Concession Approved')).toBeVisible();
  });

  test('H114: Multi-Currency FX Exposure Cockpit evaluates VaR and hedges currency', async ({ page }) => {
    await page.goto('/finance/fx-exposure-rebalancing');
    await expect(page.locator('[data-testid="fx-exposure-rebalancing-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-hedge-ratio"]')).toContainText('84.5%');
    await expect(page.locator('[data-testid="metric-max-drift"]')).toContainText('5%');

    await page.click('[data-testid="evaluate-risk-btn"]');
    await expect(page.locator('[data-testid="metric-hedge-ratio"]')).toContainText('89.2%');

    await page.click('[data-testid="execute-hedge-btn"]');
    await expect(page.locator('[data-testid="metric-hedged-notional"]')).toContainText('$7,500,000.00');
  });

  test('H115: Database Read Replica Query Router Cockpit probes lag and trips breaker', async ({ page }) => {
    await page.goto('/infrastructure/db-query-router');
    await expect(page.locator('[data-testid="db-query-router-cockpit"]')).toBeVisible();
    await expect(page.locator('[data-testid="metric-total-replicas"]')).toContainText('4');
    await expect(page.locator('[data-testid="metric-healthy-nodes"]')).toContainText('3 / 4');

    await page.click('[data-testid="probe-lag-btn"]');
    await expect(page.locator('[data-testid="metric-routing-speed"]')).toContainText('38 μs');

    await page.click('[data-testid="trip-breaker-btn"]');
    await expect(page.locator('text=Draining')).toBeVisible();
  });

});
