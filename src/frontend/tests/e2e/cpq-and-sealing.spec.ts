import { test, expect } from '@playwright/test';

test.describe('Enterprise CPQ & Cryptographic PDF Sealing E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/v1/commerce/cpq/quotes', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          quoteId: '00000000-0000-0000-0000-000000000001',
          quoteNumber: 'CPQ-2026-9042',
          customerName: 'Acme Enterprise',
          customerEmail: 'procurement@acme.com',
          currency: 'USD',
          lineItems: [],
          subtotal: 18840,
          totalDiscount: 1884,
          totalTax: 3221.64,
          grandTotal: 20177.64,
          validUntilUtc: '2026-12-01T00:00:00Z',
          status: 'DRAFT'
        })
      });
    });

    await page.route('**/api/v1/documents/**', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          documentId: '00000000-0000-0000-0000-000000000001',
          sha256Hash: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
          signatureFormat: 'ISO 32000-1 PKCS#7 / PAdES-B-LTV',
          certificateIssuer: 'CN=SGrowing Sovereign Root CA, O=SGrowing Security Grid',
          hasRfc3161Timestamp: true,
          timestampUtc: '2026-11-01T10:00:00Z',
          sealedPdfUrl: '/api/v1/documents/00000000-0000-0000-0000-000000000001/download-sealed',
          tamperProofVerificationToken: 'vtok_pkcs7_99341829031_sealed',
          isSealValid: true,
          integrityIntact: true,
          verificationTokenValid: true,
          documentSha256: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
          status: 'Valid',
          message: 'ISO 32000-1 PKCS#7 seal verified.',
          certificate: {
            certificateId: 'cert_1',
            documentId: '00000000-0000-0000-0000-000000000001',
            signerEmail: 'director@sgrowing.com',
            signerIpAddress: '197.14.120.45',
            sha256Hash: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
            timestampUtc: '2026-11-01T10:00:00Z',
            verificationUrl: 'https://verify.sgrowing.io/seal/cert_1'
          }
        })
      });
    });

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
    await expect(page.locator('header')).toContainText('PKCS#7');

    // Verify SHA-256 Digest and PKCS#7 format
    await expect(page.locator('text=SHA-256 Byte-Range Digest')).toBeVisible();
    await expect(page.locator('text=PAdES-B-LTV Qualified')).toBeVisible();

    // Load Certificate of Completion
    const certBtn = page.getByRole('button', { name: /Completion Certificate/i });
    await certBtn.click();

    // Verify Certificate rendered
    await expect(page.locator('text=ISO 32000-1 Certificate of Completion')).toBeVisible();
    await expect(page.locator('text=Audit Verification URL:')).toBeVisible();
  });
});
