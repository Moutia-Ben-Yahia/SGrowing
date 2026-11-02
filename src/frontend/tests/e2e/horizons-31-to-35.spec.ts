import { test, expect } from '@playwright/test';

test.describe('Horizons 31 to 35 Enterprise Growth OS E2E Suites', () => {

  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h31_35');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_enterprise_h31_35',
        name: 'Chief Architect & RevOps Officer',
        email: 'architect@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner']
      }));
    });
  });

  test('Horizon 31: AI Multilingual Headless CMS & Dynamic Content Block Factory', async ({ page }) => {
    await page.goto('/cms/headless-studio');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('AI Multilingual Headless CMS & Content Factory');
    await expect(page.locator('[data-testid="cms-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="articles-list"]')).toBeVisible();

    // Run brand voice evaluation
    await page.click('[data-testid="btn-run-voice-audit"]');
    await expect(page.locator('[data-testid="audit-result-card"]')).toBeVisible();
    await expect(page.locator('text=Enterprise Precision').first()).toBeVisible();

    // Open create article modal
    await page.click('[data-testid="btn-create-article"]');
    await expect(page.locator('text=New Modular Content Article').first()).toBeVisible();
    await page.click('[data-testid="btn-submit-create"]');
    await expect(page.locator('[data-testid="articles-list"]')).toContainText('Predictive ABM Intent');
  });

  test('Horizon 32: Viral Referral, Tiered Customer Loyalty & Multi-Tier Affiliate Network', async ({ page }) => {
    await page.goto('/growth/affiliates');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Viral Referral & Multi-Tier Affiliate Network');
    await expect(page.locator('[data-testid="affiliate-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="partners-list"]')).toBeVisible();
    await expect(page.locator('text=ALPHA20').first()).toBeVisible();

    // Simulate conversion payout
    await page.click('[data-testid="btn-sim-conversion"]');
    await expect(page.locator('[data-testid="sim-result-text"]')).toBeVisible();
    await expect(page.locator('[data-testid="sim-result-text"]')).toContainText('direct commission');

    // Register new partner modal
    await page.click('[data-testid="btn-register-partner"]');
    await expect(page.locator('text=Register Affiliate Partner').first()).toBeVisible();
    await page.click('[data-testid="btn-submit-partner"]');
    await expect(page.locator('[data-testid="partners-list"]')).toContainText('ScaleOps Ventures');
  });

  test('Horizon 33: Real-Time Voice Call Diarization & In-Call Copilot', async ({ page }) => {
    await page.goto('/voice/call-copilot');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Real-Time Voice Call Diarization & In-Call Copilot');
    await expect(page.locator('[data-testid="copilot-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="transcript-feed"]')).toBeVisible();
    await expect(page.locator('text=Nexus Innovations Inc').first()).toBeVisible();

    // Stream simulated utterance and check battlecard
    await page.click('[data-testid="btn-send-utterance"]');
    await expect(page.locator('[data-testid="battlecards-list"]')).toContainText('Legacy CRM Sprawl');

    // End call and summarize
    await page.click('[data-testid="btn-end-call"]');
    await expect(page.locator('[data-testid="call-summary-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="call-summary-card"]')).toContainText('Post-Call AI Summary');
  });

  test('Horizon 34: Predictive ABM Intent Surge & Territory Routing', async ({ page }) => {
    await page.goto('/sales/abm-territories');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Predictive ABM Intent Surge & Territory Routing');
    await expect(page.locator('[data-testid="abm-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="accounts-list"]')).toBeVisible();
    await expect(page.locator('text=Stripe Inc').first()).toBeVisible();

    // Auto-route unassigned account
    const routeBtn = page.locator('[data-testid="btn-route-acc_3"]');
    if (await routeBtn.isVisible()) {
      await routeBtn.click();
      await expect(page.locator('[data-testid="accounts-list"]')).toContainText('Kenji Sato');
    }

    // Ingest new surge signal
    await page.click('[data-testid="btn-ingest-surge"]');
    await expect(page.locator('text=Ingest Intent Surge Signal').first()).toBeVisible();
    await page.click('[data-testid="btn-submit-surge"]');
    await expect(page.locator('[data-testid="accounts-list"]')).toContainText('snowflake.com');
  });

  test('Horizon 35: Enterprise Zero-Trust PII Redaction & Merkle Audit Vault', async ({ page }) => {
    await page.goto('/security/compliance-vault');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Zero-Trust PII Redactor & Merkle Audit Vault');
    await expect(page.locator('[data-testid="compliance-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="audit-leaves-list"]')).toBeVisible();
    await expect(page.locator('[data-testid="residency-list"]')).toBeVisible();

    // Redact payload into synthetic tokens
    await page.click('[data-testid="btn-redact-payload"]');
    await expect(page.locator('[data-testid="redacted-result-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="redacted-result-card"]')).toContainText('[REDACTED_EMAIL_8F2A]');

    // Verify cryptographic Merkle audit proof
    await page.click('[data-testid="btn-verify-merkle"]');
    await expect(page.locator('[data-testid="merkle-verification-message"]')).toBeVisible();
    await expect(page.locator('[data-testid="merkle-verification-message"]')).toContainText('100% tamper-evident');
  });
});
