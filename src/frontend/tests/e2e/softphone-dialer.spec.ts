import { test, expect } from '@playwright/test';

test.describe('Autonomous SDR Fleet & Softphone Dialer E2E', () => {
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

  test('should press dialpad keys, initiate WebRTC call, log disposition, and classify reply', async ({ page }) => {
    await page.goto('/softphone');

    // Verify Title & Badges
    await expect(page.locator('header')).toContainText('Autonomous SDR Fleet & WebRTC Softphone');
    await expect(page.locator('header')).toContainText('AgentSDR');

    // Press Dialpad Keys
    const key5 = page.getByRole('button', { name: '5 JKL' });
    const key8 = page.getByRole('button', { name: '8 TUV' });
    await key5.click();
    await key8.click();

    // Verify dialed number updated
    const phoneInput = page.locator('input[placeholder="+213 555 019 283"]');
    await expect(phoneInput).toHaveValue(/\+213 555 019 28358/);

    // Start Outbound Call
    const startCallBtn = page.getByRole('button', { name: /Start Outbound Call/i });
    await startCallBtn.click();

    // Verify Live Call Telemetry appears with WebRTC state
    await expect(page.locator('text=CONNECTED')).toBeVisible();
    await expect(page.locator('text=Hang Up (End Call)')).toBeVisible();

    // Select Call Disposition and Submit to CRM
    const submitDispBtn = page.getByRole('button', { name: /Submit Disposition to CRM/i });
    await submitDispBtn.click();

    // Verify Call ended and returned to idle
    await expect(page.locator('text=Start Outbound Call')).toBeVisible();

    // Test AI Inbound Reply Intent Classifier
    const classifyBtn = page.getByRole('button', { name: /Classify Intent/i });
    await classifyBtn.click();
    await expect(page.locator('text=Meeting Requested')).toBeVisible();
  });
});
