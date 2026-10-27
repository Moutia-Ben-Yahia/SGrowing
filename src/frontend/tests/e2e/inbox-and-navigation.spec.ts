import { test, expect } from '@playwright/test';

test.describe('Inbox Shell & Inspiration Studios Navigation E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_1',
        email: 'admin@sgrowing.io',
        name: 'Admin E2E'
      }));
    });
  });

  test('should display all inspiration studio navigation shortcuts in inbox sidebar', async ({ page }) => {
    await page.goto('/');

    // Verify Sidebar Presence
    await expect(page.locator('aside').first()).toBeVisible();

    // Verify All 5 Inspiration Cockpit Links exist
    await expect(page.locator('a[href="/chatbots"]')).toContainText('Chatbot Flow DAG & Triggers');
    await expect(page.locator('a[href="/warmup"]')).toContainText('P2P Warmup & Spam Rescue');
    await expect(page.locator('a[href="/softphone"]')).toContainText('WebRTC Softphone & Fleet');
    await expect(page.locator('a[href="/cpq"]')).toContainText('Enterprise CPQ & EAV');
    await expect(page.locator('a[href="/sealing"]')).toContainText('Cryptographic PDF Sealing');
    await expect(page.locator('a[href="/agents/console"]')).toContainText('Autonomous AI Agent Hub');

    // Click on CPQ link and verify routing
    await page.locator('a[href="/cpq"]').click();
    await expect(page).toHaveURL(/.*\/cpq/);
    await expect(page.locator('header')).toContainText('Enterprise CPQ Studio');
  });
});
