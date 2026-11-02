import { test, expect } from '@playwright/test';

test.describe('Developer Sandbox & Rate Limiting Gateway E2E: Horizon 9', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_dev',
        email: 'developer@sgrowing.io',
        name: 'Developer Platform E2E'
      }));
    });
  });

  test('Horizon 9: Developer Sandbox Cockpit, Token Bucket & Mock Simulator', async ({ page }) => {
    await page.goto('/developer/sandbox');

    // 1. Strict LTR check
    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    // 2. Header and metadata
    await expect(page.locator('h1')).toContainText('Public Developer Sandbox & Rate Limiting Gateway');
    await expect(page.locator('text=Token-bucket rate limits')).toBeVisible();

    // 3. Rate Limiter Tier Selection & Quota
    await expect(page.locator('text=Enterprise Tier').first()).toBeVisible();
    await page.click('button:text("Starter")');
    await expect(page.locator('text=Starter Tier').first()).toBeVisible();
    await expect(page.locator('text=25 / 25')).toBeVisible();

    // 4. Overconsume to trigger 429
    await page.click('button:text("Flood 30")');
    await expect(page.locator('text=429 Too Many Requests')).toBeVisible();

    // 5. Reset Bucket
    await page.click('button:text("Reset Bucket")');
    await expect(page.locator('text=429 Too Many Requests')).not.toBeVisible();
    await expect(page.locator('text=25 / 25')).toBeVisible();

    // 6. Execute Simulated Request
    await page.click('button:text("Dispatch Simulated Request")');
    await expect(page.locator('pre')).toContainText('simulated_success');

    // 7. Role-Partitioned OpenAPI Tabs
    await page.click('button:text("admin")');
    await expect(page.locator('text=Autonomous Growth Director Planner')).toBeVisible();
  });
});
