import { test, expect } from '@playwright/test';

test.describe('Horizons 10 to 14 Enterprise Growth OS E2E Suites', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_jwt_token_for_e2e_tests');
      localStorage.setItem('sgrowing_tenant_id', '00000000-0000-0000-0000-000000000001');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'user_e2e_growth_lead',
        email: 'growth_lead@sgrowing.io',
        name: 'Growth Lead E2E'
      }));
    });
  });

  test('Horizon 10: Social Media Publisher & Content Studio', async ({ page }) => {
    await page.goto('/social/publisher');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Social Media Publisher & Content Studio');
    await expect(page.locator('text=Total Impressions').first()).toBeVisible();

    // Toggle a platform
    await page.locator('button', { hasText: 'Facebook' }).click();

    // Save Draft
    await page.click('button:text("Save Draft")');
    await expect(page.locator('text=Draft').first()).toBeVisible();

    // Signoff a pending post
    const approveBtn = page.locator('button:text("Signoff & Schedule")').first();
    if (await approveBtn.isVisible()) {
      await approveBtn.click();
      await expect(page.locator('text=Scheduled').first()).toBeVisible();
    }
  });

  test('Horizon 11: Enterprise CPQ Quotes, E-Sign & Usage Billing', async ({ page }) => {
    await page.goto('/sales/proposals');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Enterprise CPQ Quotes, E-Sign & Usage Billing');
    await expect(page.locator('text=35% Margin Guardrail Active').first()).toBeVisible();

    // Add Line Item
    await page.click('button:text("+ Add Item")');
    await expect(page.locator('table tbody tr')).toHaveCount(4);

    // Sign Proposal
    await page.click('button:text("Sign & Digitally Seal (SHA-256)")');
    await expect(page.locator('text=Digital Seal Hash:')).toBeVisible();
  });

  test('Horizon 12: Unified Omnichannel Customer Inbox & AI Deflection', async ({ page }) => {
    await page.goto('/conversations/unified');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Unified Omnichannel Customer Inbox & AI Deflection');
    await expect(page.locator('text=Active Inquiries').first()).toBeVisible();

    // Click Insert Suggestion
    await page.locator('button', { hasText: 'Insert Suggestion' }).click();
    const inputLocator = page.locator('input[placeholder="Type your omnichannel reply..."]');
    await expect(inputLocator).not.toHaveValue('');
    const inputVal = await inputLocator.inputValue();
    expect(inputVal.length).toBeGreaterThan(10);

    // Click Send
    await page.click('button:text("Send")');
    await expect(page.locator('text=Agent Support (You)').first()).toBeVisible();
  });

  test('Horizon 13: Multi-Touch Revenue Attribution & PLTV Engine', async ({ page }) => {
    await page.goto('/analytics/multi-touch');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Multi-Touch Revenue Attribution & PLTV Engine');
    await expect(page.locator('text=Blended Portfolio ROAS').first()).toBeVisible();

    // Switch Model to First-Touch
    await page.click('button:text("First-Touch")');
    await expect(page.locator('text=Model: First-Touch')).toBeVisible();

    // Switch Model to Time-Decay
    await page.click('button:text("Time-Decay (7d)")');
    await expect(page.locator('text=Model: Time-Decay (7d)')).toBeVisible();
  });

  test('Horizon 14: AI Model Governance, Firewall & Multi-LLM Router', async ({ page }) => {
    await page.goto('/ai/governance');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('AI Model Governance, Firewall & Multi-LLM Router');

    // Test DAN / Jailbreak Detection
    await page.click('button:text("DAN / Jailbreak")');
    await expect(page.locator('text=BLOCKED')).toBeVisible();
    await expect(page.locator('text=Threat Level: Critical')).toBeVisible();

    // Test PII Redaction
    await page.click('button:text("PII Leak")');
    await expect(page.locator('text=PASSED')).toBeVisible();
    await expect(page.locator('text=[EMAIL_REDACTED]')).toBeVisible();
    await expect(page.locator('text=[PHONE_REDACTED]')).toBeVisible();
  });
});
