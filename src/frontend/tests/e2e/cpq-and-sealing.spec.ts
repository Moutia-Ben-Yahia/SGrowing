import { test, expect } from '@playwright/test';

test.describe('Enterprise CPQ & Cryptographic PDF Sealing E2E', () => {
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

  test('should configure line items, calculate totals, and inspect PKCS#7 digital seal', async ({ page }) => {
    // 1. CPQ Studio
    await page.goto('/cpq');

    // Verify Title & Badges
    await expect(page.locator('header')).toContainText('Enterprise CPQ Studio & Polymorphic EAV');
    await expect(page.locator('header')).toContainText('Laravel-CRM');

    // Add Line Item
    const addItemBtn = page.getByRole('button', { name: '+ Add Line Item' });
    await addItemBtn.click();

    // Verify items in BOM
    await expect(page.locator('table tbody tr')).toHaveCount(3);
    await expect(page.locator('table tbody tr').last().locator('input').first()).toHaveValue(/SG-ADDON/);

    // Generate Formal Quote
    const generateBtn = page.getByRole('button', { name: /Generate Formal CPQ Quote/i });
    await generateBtn.click();

    // Verify Quote Number and Download button appear
    await expect(page.locator('text=Quote Number:')).toBeVisible();
    await expect(page.locator('text=Download Sealed PDF')).toBeVisible();

    // 2. Cryptographic PDF Sealing Inspector
    await page.goto('/sealing');

    // Verify Title & Badges
    await expect(page.locator('header')).toContainText('Cryptographic PDF Sealing');
    await expect(page.locator('header')).toContainText('Documenso');

    // Verify SHA-256 Checksum and PKCS#7 format
    await expect(page.locator('text=Document SHA-256 Checksum')).toBeVisible();
    await expect(page.locator('text=PKCS#7 / PAdES-B-LTV')).toBeVisible();

    // Load Certificate of Completion
    const certBtn = page.getByRole('button', { name: 'Certificate of Completion' });
    await certBtn.click();

    // Verify Certificate rendered
    await expect(page.locator('text=Proof of Execution & Signature')).toBeVisible();
    await expect(page.locator('text=Verify on Sovereign Node')).toBeVisible();
  });
});
