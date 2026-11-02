import { test, expect } from '@playwright/test';

test.describe('Horizons 26 to 30 Enterprise Growth OS E2E Suites', () => {

  test.beforeEach(async ({ page }) => {
    // Seed authenticated session
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.setItem('sgrowing_access_token', 'mock_enterprise_jwt_token_h26_30');
      localStorage.setItem('sgrowing_user', JSON.stringify({
        id: 'usr_enterprise_h26_30',
        name: 'VP Enterprise RevOps',
        email: 'revops@enterprise.corp',
        roles: ['SuperAdmin', 'OrganizationOwner']
      }));
    });
  });

  test('Horizon 26: WhatsApp Cloud Broadcast Mesh & Interactive Mobile Preview', async ({ page }) => {
    await page.goto('/outreach/whatsapp-broadcast');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('WhatsApp Cloud Broadcast Mesh');
    await expect(page.locator('text=Interactive Phone Preview').first()).toBeVisible();
    await expect(page.locator('text=⚡ Exclusive Growth OS Enterprise Upgrade').first()).toBeVisible();

    // Trigger broadcast
    await page.click('[data-testid="btn-dispatch-broadcast"]');
    await expect(page.locator('text=Completed').first()).toBeVisible();
    await expect(page.locator('text=98% Success').first()).toBeVisible();
  });

  test('Horizon 27: Multi-LLM Semantic Vector RAG & Real-Time Grounding Simulator', async ({ page }) => {
    await page.goto('/ai/knowledge-rag');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Semantic Vector RAG & Knowledge Engine');
    await expect(page.locator('[data-testid="rag-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="chunks-container"]')).toBeVisible();

    // Execute semantic search query
    await page.fill('[data-testid="input-rag-query"]', 'What is the SOC2 compliance posture?');
    await page.click('[data-testid="btn-search-rag"]');
    await expect(page.locator('[data-testid="synthesized-answer"]')).toContainText('What is the SOC2 compliance posture?');

    // Ingest new chunk via modal
    await page.click('[data-testid="btn-open-ingest"]');
    await expect(page.locator('[data-testid="ingest-modal"]')).toBeVisible();
    await page.fill('[data-testid="input-doc-title"]', 'Disaster Recovery Guide');
    await page.fill('[data-testid="input-doc-content"]', 'Automated failover completes within 60 seconds across Kubernetes pods.');
    await page.click('[data-testid="btn-confirm-ingest"]');
    await expect(page.locator('text=Disaster Recovery Guide').first()).toBeVisible();
  });

  test('Horizon 28: Programmatic SEO & Backlink Authority Mesh', async ({ page }) => {
    await page.goto('/seo/programmatic');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Programmatic SEO & Backlink Authority Mesh');
    await expect(page.locator('[data-testid="pages-list"]')).toBeVisible();
    await expect(page.locator('[data-testid="backlinks-list"]')).toBeVisible();
    await expect(page.locator('text=techcrunch.com').first()).toBeVisible();

    // Broadcast to IndexNow
    await page.click('[data-testid="btn-indexnow-submit"]');
    await expect(page.locator('[data-testid="indexnow-banner"]')).toBeVisible();

    // Batch generate new pages
    await page.click('[data-testid="btn-batch-generate"]');
    await expect(page.locator('text=Healthcare').first()).toBeVisible();
  });

  test('Horizon 29: Cross-Border FX Settlement & Dynamic Tiered Tax Compliance', async ({ page }) => {
    await page.goto('/billing/tax-compliance');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('Cross-Border FX & Tax Compliance Engine');
    await expect(page.locator('[data-testid="fx-rates-cards"]')).toBeVisible();
    await expect(page.locator('[data-testid="nexus-list"]')).toBeVisible();

    // Default is B2B with Germany -> Reverse charge
    await expect(page.locator('text=EU-VAT-REVERSE-CHARGE-ART194').first()).toBeVisible();

    // Toggle B2B off to see standard VAT applied
    await page.uncheck('[data-testid="checkbox-is-b2b"]');
    await expect(page.locator('text=STANDARD-VAT-DE').first()).toBeVisible();
  });

  test('Horizon 30: Autonomous RevOps Deal Copilot & AI Pipeline Risk Radar', async ({ page }) => {
    await page.goto('/sales/deal-copilot');

    const rootDir = await page.locator('div[dir="ltr"]').first().getAttribute('dir');
    expect(rootDir).toBe('ltr');

    await expect(page.locator('h1')).toContainText('RevOps Deal Copilot & AI Risk Radar');
    await expect(page.locator('[data-testid="deal-kpis"]')).toBeVisible();
    await expect(page.locator('[data-testid="deals-list"]')).toBeVisible();
    await expect(page.locator('text=Quantum Logistics International').first()).toBeVisible();

    // Execute intervention on Quantum Logistics
    const interveneBtn = page.locator('[data-testid="btn-intervene-deal_quantum_corp"]');
    await interveneBtn.click();

    await expect(page.locator('[data-testid="deal-toast"]')).toBeVisible();
    await expect(page.locator('text=Win probability lifted by +20%')).toBeVisible();
  });

});
