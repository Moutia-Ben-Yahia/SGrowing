import { test, expect } from '@playwright/test';

test.describe('P2P Mailbox Warmup & Deliverability Dashboard E2E', () => {
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

  test('should load warmup peers, run spam rescue sweep, and audit DNS', async ({ page }) => {
    await page.goto('/warmup');

    // Verify Title & Badges
    await expect(page.locator('header')).toContainText('P2P Mailbox Warmup & Spam Rescue Sweeper');
    await expect(page.locator('header')).toContainText('Warmbly');

    // Verify Deliverability Health Cards
    await expect(page.locator('text=Active Peer Pairs')).toBeVisible();
    await expect(page.locator('text=Rescued from Spam/Junk')).toBeVisible();
    await expect(page.locator('text=Deliverability Score')).toBeVisible();

    // Verify P2P Table renders mailboxes
    await expect(page.locator('text=Organic P2P Email Exchange Mesh')).toBeVisible();

    // Trigger DNS Audit
    const auditBtn = page.getByRole('button', { name: 'Audit' });
    await auditBtn.click();
    await expect(page.locator('text=SPF Authentication')).toBeVisible();
    await expect(page.getByText('2048-BIT', { exact: true })).toBeVisible();

    // Run Spam Rescue Sweep
    const sweepBtn = page.getByRole('button', { name: /Run Spam Rescue Sweep/i });
    await sweepBtn.click();
    await expect(page.locator('text=Moved to Inbox')).toBeVisible();
  });
});
