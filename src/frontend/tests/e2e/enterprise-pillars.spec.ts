import { test, expect } from '@playwright/test';

test.describe('Enterprise Pillars Cockpits E2E', () => {
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

  test('should render Cold Outreach & Spintax Engine cockpit', async ({ page }) => {
    await page.goto('/outbound/spintax');
    await expect(page.locator('header')).toContainText('Cold Outreach AI SDR & Spintax Engine');
    await expect(page.locator('button:has-text("Compile Deterministic Variants")')).toBeVisible();
    await expect(page.locator('button:has-text("Real-time MX Verifier")')).toBeVisible();
    await expect(page.locator('button:has-text("B2B Contact Enrichment")')).toBeVisible();
  });

  test('should render Warmup Swarm & Seed Placement Radar cockpit', async ({ page }) => {
    await page.goto('/deliverability/warmup-swarm');
    await expect(page.locator('header')).toContainText('Warmup Swarm & ESP Seed Placement Radar');
    await expect(page.locator('button:has-text("Simulate Thread Exchange")')).toBeVisible();
    await expect(page.locator('button:has-text("ESP Placement Radar")')).toBeVisible();
  });

  test('should render Omnichannel MIME & Macro cockpit', async ({ page }) => {
    await page.goto('/inbox/omnichannel');
    await expect(page.locator('header')).toContainText('Omnichannel Power Tools: MIME, Macros & SLA');
    await expect(page.locator('button:has-text("Canned Macros")')).toBeVisible();
    await expect(page.locator('button:has-text("RFC 822 MIME Parser")')).toBeVisible();
  });

  test('should render High-Throughput CSV Import cockpit', async ({ page }) => {
    await page.goto('/accounts/csv-import');
    await expect(page.locator('header')).toContainText('High-Throughput CSV Importer & Duplicate Resolver');
    await expect(page.locator('button:has-text("1. Parse & Preview CSV")')).toBeVisible();
    await expect(page.locator('button:has-text("2. Run Transactional Import")')).toBeVisible();
  });

  test('should render Bot Flow Interactive Runner cockpit', async ({ page }) => {
    await page.goto('/chatbots/simulator');
    await expect(page.locator('header')).toContainText('Interactive Bot Flow Simulator & Step Runner');
    await expect(page.locator('button:has-text("Restart Simulation")')).toBeVisible();
  });

  test('should render Multi-Party E-Signature & OTP Orchestrator cockpit', async ({ page }) => {
    await page.goto('/contracts/multi-party');
    await expect(page.locator('header')).toContainText('Multi-Party Cryptographic E-Signature & OTP Orchestrator');
    await expect(page.locator('button:has-text("New Multi-Signer Agreement")')).toBeVisible();
    await expect(page.locator('text=Multi-Party Signer Pipeline')).toBeVisible();
  });

  test('should render Multi-Carrier COD & Anti-Fraud cockpit', async ({ page }) => {
    await page.goto('/courier/cod-fraud');
    await expect(page.locator('header')).toContainText('Regional Courier Gateway & COD Anti-Fraud Radar');
    await expect(page.locator('button:has-text("Carrier Shipping Gateway")')).toBeVisible();
    await expect(page.locator('button:has-text("COD Anti-Fraud Radar")')).toBeVisible();
  });
});
